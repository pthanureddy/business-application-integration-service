using Integration.Api.Contracts;
using Integration.Api.Domain;
using Integration.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Integration.Api.Services;

public sealed class AccountsPayableWorkflowService(
    IntegrationDbContext database,
    TimeProvider timeProvider)
{
    public async Task<AccountsPayableInvoice> SubmitAsync(
        AccountsPayableInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var supplierNumber = request.SupplierNumber.Trim().ToUpperInvariant();
        var invoiceNumber = request.InvoiceNumber.Trim().ToUpperInvariant();
        var duplicate = await database.AccountsPayableInvoices.AnyAsync(
            invoice => invoice.SupplierNumber == supplierNumber
                && invoice.InvoiceNumber == invoiceNumber,
            cancellationToken);

        if (duplicate)
        {
            throw new AccountsPayableDuplicateInvoiceException(supplierNumber, invoiceNumber);
        }

        var supplier = await database.SupplierMasterRecords.SingleOrDefaultAsync(
            record => record.SupplierNumber == supplierNumber && record.IsActive,
            cancellationToken);
        var exceptions = DetermineExceptions(request, supplier);
        var approvalRole = request.GrossAmount switch
        {
            >= 100_000m => "FinanceManagerAndCfo",
            >= 25_000m => "FinanceManager",
            _ => "CostCenterOwner"
        };

        var invoice = AccountsPayableInvoice.Create(
            supplierNumber,
            invoiceNumber,
            request.IssuedOn,
            request.DueOn,
            request.Currency.Trim().ToUpperInvariant(),
            request.GrossAmount,
            NormalizeOptional(request.PurchaseOrderReference),
            NormalizeOptional(request.RequestedAccount),
            NormalizeOptional(request.RequestedCostCenter),
            supplier?.DefaultAccount,
            supplier?.DefaultCostCenter,
            approvalRole,
            exceptions,
            timeProvider.GetUtcNow());

        database.AccountsPayableInvoices.Add(invoice);
        await database.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task<AccountsPayableInvoice> GetAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        return await database.AccountsPayableInvoices.SingleOrDefaultAsync(
                invoice => invoice.Id == invoiceId,
                cancellationToken)
            ?? throw new AccountsPayableInvoiceNotFoundException(invoiceId);
    }

    public async Task<IReadOnlyList<AccountsPayableInvoice>> GetExceptionsAsync(
        CancellationToken cancellationToken)
    {
        return await database.AccountsPayableInvoices
            .Where(invoice => invoice.Status == AccountsPayableInvoiceStatus.Exception)
            .OrderBy(invoice => invoice.DueOn)
            .ThenBy(invoice => invoice.SupplierNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountsPayableInvoice> ApproveAsync(
        Guid invoiceId,
        AccountsPayableDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await GetAsync(invoiceId, cancellationToken);
        invoice.Approve(request.ApproverRole.Trim(), request.DecisionBy.Trim(), timeProvider.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task<AccountsPayableInvoice> RejectAsync(
        Guid invoiceId,
        AccountsPayableDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await GetAsync(invoiceId, cancellationToken);
        invoice.Reject(request.DecisionBy.Trim(), timeProvider.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task<AccountsPayableAnalyticsResponse> GetAnalyticsAsync(
        CancellationToken cancellationToken)
    {
        var total = await database.AccountsPayableInvoices.CountAsync(cancellationToken);
        var exceptions = await database.AccountsPayableInvoices.CountAsync(
            invoice => invoice.Status == AccountsPayableInvoiceStatus.Exception,
            cancellationToken);
        var awaiting = await database.AccountsPayableInvoices.CountAsync(
            invoice => invoice.Status == AccountsPayableInvoiceStatus.AwaitingApproval,
            cancellationToken);
        var ready = await database.AccountsPayableInvoices.CountAsync(
            invoice => invoice.Status == AccountsPayableInvoiceStatus.ReadyForPayment,
            cancellationToken);
        var touchless = await database.AccountsPayableInvoices.CountAsync(
            invoice => invoice.TouchlessCandidate,
            cancellationToken);
        var rate = total == 0
            ? 0m
            : decimal.Round(touchless * 100m / total, 2, MidpointRounding.AwayFromZero);

        return new AccountsPayableAnalyticsResponse(
            total,
            exceptions,
            awaiting,
            ready,
            touchless,
            rate);
    }

    public static void SeedSyntheticSupplierMaster(IntegrationDbContext database)
    {
        if (database.SupplierMasterRecords.Any())
        {
            return;
        }

        database.SupplierMasterRecords.AddRange(
            new SupplierMasterRecord("SUP-100", "Nordic Logistics AB", "5400", "LOG-01", 30),
            new SupplierMasterRecord("SUP-200", "Facility Services AB", "5060", "PROP-01", 30),
            new SupplierMasterRecord("SUP-300", "Inactive Supplier AB", "6990", "ADMIN-01", 30, false));
        database.SaveChanges();
    }

    private static IReadOnlyCollection<string> DetermineExceptions(
        AccountsPayableInvoiceRequest request,
        SupplierMasterRecord? supplier)
    {
        var exceptions = new HashSet<string>(StringComparer.Ordinal);
        if (supplier is null)
        {
            exceptions.Add("SUPPLIER_NOT_FOUND_OR_INACTIVE");
            return exceptions;
        }

        if (string.IsNullOrWhiteSpace(request.PurchaseOrderReference))
        {
            exceptions.Add("PURCHASE_ORDER_REFERENCE_MISSING");
        }

        if (!string.IsNullOrWhiteSpace(request.RequestedAccount)
            && !string.Equals(
                request.RequestedAccount.Trim(),
                supplier.DefaultAccount,
                StringComparison.OrdinalIgnoreCase))
        {
            exceptions.Add("ACCOUNT_DEVIATION");
        }

        if (!string.IsNullOrWhiteSpace(request.RequestedCostCenter)
            && !string.Equals(
                request.RequestedCostCenter.Trim(),
                supplier.DefaultCostCenter,
                StringComparison.OrdinalIgnoreCase))
        {
            exceptions.Add("COST_CENTER_DEVIATION");
        }

        return exceptions;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }
}
