using Integration.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Integration.Api.Persistence;

public sealed class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options)
    : DbContext(options)
{
    public DbSet<IntegrationJob> IntegrationJobs => Set<IntegrationJob>();

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
    }
}
