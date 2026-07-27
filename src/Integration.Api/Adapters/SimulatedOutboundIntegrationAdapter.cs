using System.Security.Cryptography;
using System.Text;
using Integration.Api.Domain;

namespace Integration.Api.Adapters;

public sealed class SimulatedOutboundIntegrationAdapter : IOutboundIntegrationAdapter
{
    public Task<string> SendAsync(
        IntegrationKind kind,
        string canonicalPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}:{canonicalPayload}")));
        return Task.FromResult($"SIM-{kind.ToString().ToUpperInvariant()}-{digest[..10]}");
    }
}

