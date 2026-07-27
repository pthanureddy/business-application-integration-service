namespace Integration.Api.Domain;

public sealed class IntegrationJob
{
    private IntegrationJob()
    {
    }

    private IntegrationJob(
        Guid id,
        IntegrationKind kind,
        string idempotencyKey,
        string sourceSystem,
        string correlationId,
        string canonicalPayload,
        string payloadSha256,
        DateTimeOffset now)
    {
        Id = id;
        Kind = kind;
        IdempotencyKey = idempotencyKey;
        SourceSystem = sourceSystem;
        CorrelationId = correlationId;
        CanonicalPayload = canonicalPayload;
        PayloadSha256 = payloadSha256;
        Status = IntegrationStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public IntegrationKind Kind { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string SourceSystem { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public string CanonicalPayload { get; private set; } = string.Empty;

    public string PayloadSha256 { get; private set; } = string.Empty;

    public IntegrationStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? ExternalReference { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static IntegrationJob Create(
        IntegrationKind kind,
        string idempotencyKey,
        string sourceSystem,
        string correlationId,
        string canonicalPayload,
        string payloadSha256,
        DateTimeOffset now)
    {
        return new IntegrationJob(
            Guid.NewGuid(),
            kind,
            idempotencyKey,
            sourceSystem,
            correlationId,
            canonicalPayload,
            payloadSha256,
            now);
    }

    public void StartDispatch(DateTimeOffset now)
    {
        if (Status is IntegrationStatus.Succeeded)
        {
            throw new InvalidOperationException("A completed integration job cannot be dispatched again.");
        }

        Status = IntegrationStatus.Dispatching;
        AttemptCount++;
        LastError = null;
        UpdatedAt = now;
    }

    public void Complete(string externalReference, DateTimeOffset now)
    {
        if (Status is not IntegrationStatus.Dispatching)
        {
            throw new InvalidOperationException("Only a dispatching integration job can be completed.");
        }

        Status = IntegrationStatus.Succeeded;
        ExternalReference = externalReference;
        LastError = null;
        UpdatedAt = now;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        if (Status is not IntegrationStatus.Dispatching)
        {
            throw new InvalidOperationException("Only a dispatching integration job can fail.");
        }

        Status = IntegrationStatus.Failed;
        LastError = error;
        UpdatedAt = now;
    }
}

