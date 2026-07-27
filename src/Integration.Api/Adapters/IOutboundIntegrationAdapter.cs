using Integration.Api.Domain;

namespace Integration.Api.Adapters;

public interface IOutboundIntegrationAdapter
{
    Task<string> SendAsync(
        IntegrationKind kind,
        string canonicalPayload,
        string correlationId,
        CancellationToken cancellationToken);
}

