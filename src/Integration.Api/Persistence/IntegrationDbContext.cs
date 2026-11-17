using Integration.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Integration.Api.Persistence;

public sealed class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options)
    : DbContext(options)
{
    public DbSet<IntegrationJob> IntegrationJobs => Set<IntegrationJob>();

    public DbSet<SupplierMasterRecord> SupplierMasterRecords => Set<SupplierMasterRecord>();

    public DbSet<AccountsPayableInvoice> AccountsPayableInvoices => Set<AccountsPayableInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<IntegrationJob>();
        job.ToTable("integration_jobs");
        job.HasKey(x => x.Id);
        job.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        job.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        job.Property(x => x.IdempotencyKey).HasMaxLength(160);
        job.Property(x => x.SourceSystem).HasMaxLength(100);
        job.Property(x => x.CorrelationId).HasMaxLength(100);
        job.Property(x => x.PayloadSha256).HasMaxLength(64);
        job.Property(x => x.ExternalReference).HasMaxLength(200);
        job.Property(x => x.LastError).HasMaxLength(1000);
        job.HasIndex(x => new { x.Kind, x.IdempotencyKey }).IsUnique();
        job.HasIndex(x => new { x.Status, x.UpdatedAt });

        var supplier = modelBuilder.Entity<SupplierMasterRecord>();
        supplier.ToTable("supplier_master");
        supplier.HasKey(x => x.SupplierNumber);
        supplier.Property(x => x.SupplierNumber).HasMaxLength(100);
        supplier.Property(x => x.LegalName).HasMaxLength(200);
        supplier.Property(x => x.DefaultAccount).HasMaxLength(40);
        supplier.Property(x => x.DefaultCostCenter).HasMaxLength(40);

        var invoice = modelBuilder.Entity<AccountsPayableInvoice>();
        invoice.ToTable("accounts_payable_invoices");
        invoice.HasKey(x => x.Id);
        invoice.Property(x => x.SupplierNumber).HasMaxLength(100);
        invoice.Property(x => x.InvoiceNumber).HasMaxLength(100);
        invoice.Property(x => x.Currency).HasMaxLength(3);
        invoice.Property(x => x.PurchaseOrderReference).HasMaxLength(100);
        invoice.Property(x => x.RequestedAccount).HasMaxLength(40);
        invoice.Property(x => x.RequestedCostCenter).HasMaxLength(40);
        invoice.Property(x => x.SuggestedAccount).HasMaxLength(40);
        invoice.Property(x => x.SuggestedCostCenter).HasMaxLength(40);
        invoice.Property(x => x.RequiredApprovalRole).HasMaxLength(100);
        invoice.Property(x => x.DecisionBy).HasMaxLength(160);
        invoice.Property(x => x.ExceptionCodes).HasMaxLength(1000);
        invoice.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        invoice.HasIndex(x => new { x.SupplierNumber, x.InvoiceNumber }).IsUnique();
        invoice.HasIndex(x => new { x.Status, x.DueOn });
    }
}
