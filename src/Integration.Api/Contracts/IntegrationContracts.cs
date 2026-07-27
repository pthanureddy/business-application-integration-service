using Integration.Api.Domain;

namespace Integration.Api.Contracts;

public sealed record ShipmentIntegrationRequest(
    string SourceSystem,
    string ShipmentId,
    string OrderReference,
    string CarrierCode,
    string? TrackingNumber,
    string Status,
    DateTimeOffset OccurredAtUtc,
    string DestinationCountry);

public sealed record ElectronicInvoiceLine(
    string ItemCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate);

public sealed record ElectronicInvoiceRequest(
    string SourceSystem,
    string InvoiceNumber,
    string CustomerCode,
    string Currency,
    DateOnly IssuedOn,
    decimal TotalAmount,
    IReadOnlyList<ElectronicInvoiceLine> Lines);

public sealed record IntegrationJobResponse(
    Guid Id,
    IntegrationKind Kind,
    IntegrationStatus Status,
    bool Duplicate,
    int AttemptCount,
    string CorrelationId,
    string? ExternalReference,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static IntegrationJobResponse From(IntegrationJob job, bool duplicate = false)
    {
        return new IntegrationJobResponse(
            job.Id,
            job.Kind,
            job.Status,
            duplicate,
            job.AttemptCount,
            job.CorrelationId,
            job.ExternalReference,
            job.LastError,
            job.CreatedAt,
            job.UpdatedAt);
    }
}

public sealed record SapSalesOrderResponse(
    int DocEntry,
    int DocNum,
    string CardCode,
    decimal DocTotal,
    string Currency,
    DateTimeOffset UpdatedAtUtc);

