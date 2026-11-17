using Integration.Api.Domain;

namespace Integration.Api.Contracts;

public sealed record AccountsPayableInvoiceRequest(
    string SupplierNumber,
    string InvoiceNumber,
    DateOnly IssuedOn,
    DateOnly DueOn,
    string Currency,
    decimal GrossAmount,
    string? PurchaseOrderReference,
    string? RequestedAccount,
    string? RequestedCostCenter);

public sealed record AccountsPayableDecisionRequest(
    string ApproverRole,
    string DecisionBy);

public sealed record AccountsPayableInvoiceResponse(
    Guid Id,
    string SupplierNumber,
    string InvoiceNumber,
    DateOnly IssuedOn,
    DateOnly DueOn,
    string Currency,
    decimal GrossAmount,
    string? PurchaseOrderReference,
    string? RequestedAccount,
    string? RequestedCostCenter,
    string? SuggestedAccount,
    string? SuggestedCostCenter,
    string RequiredApprovalRole,
    string? DecisionBy,
    IReadOnlyList<string> ExceptionCodes,
    bool TouchlessCandidate,
    AccountsPayableInvoiceStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AccountsPayableInvoiceResponse From(AccountsPayableInvoice invoice)
    {
        return new AccountsPayableInvoiceResponse(
            invoice.Id,
            invoice.SupplierNumber,
            invoice.InvoiceNumber,
            invoice.IssuedOn,
            invoice.DueOn,
            invoice.Currency,
            invoice.GrossAmount,
            invoice.PurchaseOrderReference,
            invoice.RequestedAccount,
            invoice.RequestedCostCenter,
            invoice.SuggestedAccount,
            invoice.SuggestedCostCenter,
            invoice.RequiredApprovalRole,
            invoice.DecisionBy,
            invoice.GetExceptionCodes(),
            invoice.TouchlessCandidate,
            invoice.Status,
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }
}

public sealed record AccountsPayableAnalyticsResponse(
    int TotalInvoices,
    int ExceptionInvoices,
    int AwaitingApprovalInvoices,
    int ReadyForPaymentInvoices,
    int TouchlessCandidates,
    decimal TouchlessCandidateRatePercent);
