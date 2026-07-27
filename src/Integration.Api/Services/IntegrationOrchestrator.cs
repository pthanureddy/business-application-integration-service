using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Integration.Api.Adapters;
using Integration.Api.Contracts;
using Integration.Api.Domain;
using Integration.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Integration.Api.Services;

public sealed class IntegrationOrchestrator(
    IntegrationDbContext dbContext,
    IOutboundIntegrationAdapter adapter,
    TimeProvider timeProvider,
    ILogger<IntegrationOrchestrator> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SubmissionResult> SubmitAsync(
        IntegrationKind kind,
        string sourceSystem,
        string idempotencyKey,
        string correlationId,
        object canonicalPayload,
        CancellationToken cancellationToken)
    {
        var canonicalJson = JsonSerializer.Serialize(canonicalPayload, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

        var existing = await dbContext.IntegrationJobs.SingleOrDefaultAsync(
            job => job.Kind == kind && job.IdempotencyKey == idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadSha256, hash, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException(idempotencyKey);
            }

            logger.LogInformation(
                "Returning existing {IntegrationKind} job {JobId} for idempotency key",
                kind,
                existing.Id);
            return new SubmissionResult(existing, Duplicate: true);
        }

        var now = timeProvider.GetUtcNow();
        var job = IntegrationJob.Create(
            kind,
            idempotencyKey,
            sourceSystem.Trim(),
            correlationId,
            canonicalJson,
            hash,
            now);
        dbContext.IntegrationJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        await DispatchAsync(job, cancellationToken);
        return new SubmissionResult(job, Duplicate: false);
    }

    public async Task<IntegrationJob> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return await dbContext.IntegrationJobs.SingleOrDefaultAsync(
                job => job.Id == jobId,
                cancellationToken)
            ?? throw new IntegrationJobNotFoundException(jobId);
    }

    public async Task<IntegrationJob> RetryAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await GetAsync(jobId, cancellationToken);
        if (job.Status is not IntegrationStatus.Failed)
        {
            throw new IntegrationJobStateException(job.Id, job.Status.ToString());
        }

        await DispatchAsync(job, cancellationToken);
        return job;
    }

    private async Task DispatchAsync(IntegrationJob job, CancellationToken cancellationToken)
    {
        job.StartDispatch(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var externalReference = await adapter.SendAsync(
                job.Kind,
                job.CanonicalPayload,
                job.CorrelationId,
                cancellationToken);
            job.Complete(externalReference, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Integration job {JobId} completed for {IntegrationKind} on attempt {AttemptCount}",
                job.Id,
                job.Kind,
                job.AttemptCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var safeError = exception is IntegrationAdapterException
                ? exception.Message
                : "The outbound adapter failed before confirming delivery.";
            job.Fail(safeError, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Integration job {JobId} failed for {IntegrationKind} on attempt {AttemptCount}",
                job.Id,
                job.Kind,
                job.AttemptCount);
            throw new ExternalIntegrationException(
                job.Id,
                "Outbound integration failed. The job is retained for investigation and retry.",
                exception);
        }
    }
}

public sealed record SubmissionResult(IntegrationJob Job, bool Duplicate);

