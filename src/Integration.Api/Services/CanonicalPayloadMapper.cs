using Integration.Api.Contracts;

namespace Integration.Api.Services;

public static class CanonicalPayloadMapper
{
    public static CanonicalShipment Map(ShipmentIntegrationRequest request)
    {
        return new CanonicalShipment(
            SchemaVersion: "1.0",
            SourceSystem: request.SourceSystem.Trim(),
            ShipmentId: request.ShipmentId.Trim(),
            OrderReference: request.OrderReference.Trim(),
            CarrierCode: request.CarrierCode.Trim().ToUpperInvariant(),
            TrackingNumber: request.TrackingNumber?.Trim(),
            Status: request.Status.Trim().ToUpperInvariant(),
            OccurredAtUtc: request.OccurredAtUtc.ToUniversalTime(),
            DestinationCountry: request.DestinationCountry.Trim().ToUpperInvariant());
    }

    public static CanonicalElectronicInvoice Map(ElectronicInvoiceRequest request)
    {
        return new CanonicalElectronicInvoice(
            SchemaVersion: "1.0",
            SourceSystem: request.SourceSystem.Trim(),
            InvoiceNumber: request.InvoiceNumber.Trim(),
            CustomerCode: request.CustomerCode.Trim(),
            Currency: request.Currency.Trim().ToUpperInvariant(),
            IssuedOn: request.IssuedOn,
            TotalAmount: decimal.Round(request.TotalAmount, 2, MidpointRounding.AwayFromZero),
            Lines: request.Lines.Select(
                    line => new CanonicalElectronicInvoiceLine(
                        line.ItemCode.Trim(),
                        line.Quantity,
                        decimal.Round(line.UnitPrice, 2, MidpointRounding.AwayFromZero),
                        line.TaxRate))
                .ToList());
    }
}

public sealed record CanonicalShipment(
    string SchemaVersion,
    string SourceSystem,
    string ShipmentId,
    string OrderReference,
    string CarrierCode,
    string? TrackingNumber,
    string Status,
    DateTimeOffset OccurredAtUtc,
    string DestinationCountry);

public sealed record CanonicalElectronicInvoice(
    string SchemaVersion,
    string SourceSystem,
    string InvoiceNumber,
    string CustomerCode,
    string Currency,
    DateOnly IssuedOn,
    decimal TotalAmount,
    IReadOnlyList<CanonicalElectronicInvoiceLine> Lines);

public sealed record CanonicalElectronicInvoiceLine(
    string ItemCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate);

