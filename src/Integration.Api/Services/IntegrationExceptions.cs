namespace Integration.Api.Services;

public sealed class IntegrationJobNotFoundException(Guid jobId)
    : Exception($"Integration job was not found: {jobId}")
{
    public Guid JobId { get; } = jobId;
}

public sealed class IdempotencyConflictException(string idempotencyKey)
    : Exception($"Idempotency key '{idempotencyKey}' was already used with different content.");

public sealed class IntegrationJobStateException(Guid jobId, string status)
    : Exception($"Integration job {jobId} cannot be retried from status {status}.");

public sealed class ExternalIntegrationException(Guid jobId, string message, Exception innerException)
    : Exception(message, innerException)
{
    public Guid JobId { get; } = jobId;
}

