namespace Integration.Api.Services;

public sealed class AccountsPayableInvoiceNotFoundException(Guid invoiceId)
    : Exception($"Accounts-payable invoice was not found: {invoiceId}");

public sealed class AccountsPayableDuplicateInvoiceException(
    string supplierNumber,
    string invoiceNumber)
    : Exception($"Supplier invoice '{supplierNumber}/{invoiceNumber}' already exists.");

public sealed class AccountsPayableInvoiceStateException(Guid invoiceId, string status)
    : Exception($"Accounts-payable invoice {invoiceId} cannot be changed from status {status}.");

public sealed class AccountsPayableApprovalRoleException(
    Guid invoiceId,
    string requiredRole,
    string providedRole)
    : Exception(
        $"Invoice {invoiceId} requires approval role '{requiredRole}', not '{providedRole}'.");
