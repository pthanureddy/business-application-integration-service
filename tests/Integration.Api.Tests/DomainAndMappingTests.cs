using Integration.Api.Adapters;
using Integration.Api.Contracts;
using Integration.Api.Domain;
using Integration.Api.Services;

namespace Integration.Api.Tests;

public sealed class DomainAndMappingTests
{
    [Fact]
    public void ShipmentMappingNormalizesBusinessCodes()
    {
        var request = new ShipmentIntegrationRequest(
            " warehouse ",
            " SHP-1 ",
            " SO-1 ",
            " dhl ",
            " TRACK-1 ",
            " delivered ",
            DateTimeOffset.Parse("2026-07-27T10:00:00+02:00"),
            " es ");

        var canonical = CanonicalPayloadMapper.Map(request);

        Assert.Equal("warehouse", canonical.SourceSystem);
        Assert.Equal("DHL", canonical.CarrierCode);
        Assert.Equal("DELIVERED", canonical.Status);
        Assert.Equal("ES", canonical.DestinationCountry);
        Assert.Equal(DateTimeOffset.Parse("2026-07-27T08:00:00Z"), canonical.OccurredAtUtc);
    }

    [Fact]
    public void InvoiceValidationAcceptsMatchingTaxInclusiveTotal()
    {
        var request = new ElectronicInvoiceRequest(
            "billing",
            "INV-1",
            "C-1",
            "eur",
            new DateOnly(2026, 7, 27),
            250m,
            [new ElectronicInvoiceLine("ITEM-1", 2, 100m, 25m)]);

        var errors = RequestValidation.Validate(request);
        var canonical = CanonicalPayloadMapper.Map(request);

        Assert.Empty(errors);
        Assert.Equal("EUR", canonical.Currency);
        Assert.Equal(250m, canonical.TotalAmount);
    }

    [Fact]
    public void ODataQueryContainsSelectedFieldsFilterAndOrdering()
    {
        var path = ODataQueryBuilder.BuildUpdatedSalesOrdersPath(
            DateTimeOffset.Parse("2026-07-27T10:30:00+02:00"));
        var decoded = Uri.UnescapeDataString(path);

        Assert.StartsWith("/b1s/v2/Orders?", path, StringComparison.Ordinal);
        Assert.Contains("$select=DocEntry,DocNum,CardCode,DocTotal,DocCurrency,UpdateDate", decoded);
        Assert.Contains("$filter=UpdateDate ge '2026-07-27T08:30:00Z'", decoded);
        Assert.Contains("$orderby=UpdateDate asc", decoded);
    }

    [Fact]
    public void IntegrationJobTracksFailureAndSuccessfulRetry()
    {
        var now = DateTimeOffset.Parse("2026-07-27T08:00:00Z");
        var job = IntegrationJob.Create(
            IntegrationKind.LogisticsShipment,
            "idem-1",
            "wms",
            "corr-1",
            "{}",
            new string('A', 64),
            now);

        job.StartDispatch(now.AddMinutes(1));
        job.Fail("timeout", now.AddMinutes(2));
        job.StartDispatch(now.AddMinutes(3));
        job.Complete("EXT-1", now.AddMinutes(4));

        Assert.Equal(IntegrationStatus.Succeeded, job.Status);
        Assert.Equal(2, job.AttemptCount);
        Assert.Equal("EXT-1", job.ExternalReference);
        Assert.Null(job.LastError);
    }

    [Fact]
    public void CompletedJobCannotBeDispatchedAgain()
    {
        var now = DateTimeOffset.Parse("2026-07-27T08:00:00Z");
        var job = IntegrationJob.Create(
            IntegrationKind.ElectronicInvoice,
            "idem-2",
            "billing",
            "corr-2",
            "{}",
            new string('B', 64),
            now);
        job.StartDispatch(now);
        job.Complete("EXT-2", now);

        var exception = Assert.Throws<InvalidOperationException>(
            () => job.StartDispatch(now.AddMinutes(1)));

        Assert.Contains("cannot be dispatched again", exception.Message, StringComparison.Ordinal);
    }
}
