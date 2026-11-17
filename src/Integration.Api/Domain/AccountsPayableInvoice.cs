using Integration.Api.Services;

namespace Integration.Api.Domain;

public sealed class AccountsPayableInvoice
{
    private AccountsPayableInvoice()
    {
    }

    private AccountsPayableInvoice(
        Guid id,
        string supplierNumber,
        string invoiceNumber,
        DateOnly issuedOn,
        DateOnly dueOn,
        string currency,
        decimal grossAmount,
        string? purchaseOrderReference,
        string? requestedAccount,
        string? requestedCostCenter,
        string? suggestedAccount,
        string? suggestedCostCenter,
        string requiredApprovalRole,
        string exceptionCodes,
        bool touchlessCandidate,
        DateTimeOffset now)
    {
        Id = id;
        SupplierNumber = supplierNumber;
        InvoiceNumber = invoiceNumber;
        IssuedOn = issuedOn;
        DueOn = dueOn;
        Currency = currency;
        GrossAmount = grossAmount;
        PurchaseOrderReference = purchaseOrderReference;
        RequestedAccount = requestedAccount;
        RequestedCostCenter = requestedCostCenter;
        SuggestedAccount = suggestedAccount;
        SuggestedCostCenter = suggestedCostCenter;
        RequiredApprovalRole = requiredApprovalRole;
        ExceptionCodes = exceptionCodes;
        TouchlessCandidate = touchlessCandidate;
        Status = string.IsNullOrEmpty(exceptionCodes)
            ? AccountsPayableInvoiceStatus.AwaitingApproval
            : AccountsPayableInvoiceStatus.Exception;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string SupplierNumber { get; private set; } = string.Empty;

    public string InvoiceNumber { get; private set; } = string.Empty;

    public DateOnly IssuedOn { get; private set; }

    public DateOnly DueOn { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal GrossAmount { get; private set; }

    public string? PurchaseOrderReference { get; private set; }

    public string? RequestedAccount { get; private set; }

    public string? RequestedCostCenter { get; private set; }

    public string? SuggestedAccount { get; private set; }

    public string? SuggestedCostCenter { get; private set; }

    public string RequiredApprovalRole { get; private set; } = string.Empty;

    public string? DecisionBy { get; private set; }

    public string ExceptionCodes { get; private set; } = string.Empty;

    public bool TouchlessCandidate { get; private set; }

    public AccountsPayableInvoiceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static AccountsPayableInvoice Create(
        string supplierNumber,
        string invoiceNumber,
        DateOnly issuedOn,
        DateOnly dueOn,
        string currency,
        decimal grossAmount,
        string? purchaseOrderReference,
        string? requestedAccount,
        string? requestedCostCenter,
        string? suggestedAccount,
        string? suggestedCostCenter,
        string requiredApprovalRole,
        IReadOnlyCollection<string> exceptionCodes,
        DateTimeOffset now)
    {
        return new AccountsPayableInvoice(
            Guid.NewGuid(),
            supplierNumber,
            invoiceNumber,
            issuedOn,
            dueOn,
            currency,
            grossAmount,
            purchaseOrderReference,
            requestedAccount,
            requestedCostCenter,
            suggestedAccount,
            suggestedCostCenter,
            requiredApprovalRole,
            string.Join('|', exceptionCodes.Order(StringComparer.Ordinal)),
            exceptionCodes.Count == 0,
            now);
    }

    public IReadOnlyList<string> GetExceptionCodes()
    {
        return string.IsNullOrEmpty(ExceptionCodes)
            ? []
            : ExceptionCodes.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }

    public void Approve(string approverRole, string decisionBy, DateTimeOffset now)
    {
        if (Status is not AccountsPayableInvoiceStatus.AwaitingApproval)
        {
            throw new AccountsPayableInvoiceStateException(Id, Status.ToString());
        }

        if (!string.Equals(approverRole, RequiredApprovalRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new AccountsPayableApprovalRoleException(Id, RequiredApprovalRole, approverRole);
        }

        Status = AccountsPayableInvoiceStatus.ReadyForPayment;
        DecisionBy = decisionBy.Trim();
        UpdatedAt = now;
    }

    public void Reject(string decisionBy, DateTimeOffset now)
    {
        if (Status is not AccountsPayableInvoiceStatus.AwaitingApproval)
        {
            throw new AccountsPayableInvoiceStateException(Id, Status.ToString());
        }

        Status = AccountsPayableInvoiceStatus.Rejected;
        DecisionBy = decisionBy.Trim();
        UpdatedAt = now;
    }
}
