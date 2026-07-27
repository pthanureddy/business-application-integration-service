using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Integration.Api.Configuration;
using Integration.Api.Domain;
using Microsoft.Extensions.Options;

namespace Integration.Api.Adapters;

public sealed class HttpOutboundIntegrationAdapter(
    IHttpClientFactory httpClientFactory,
    IOptions<OutboundOptions> options,
    ILogger<HttpOutboundIntegrationAdapter> logger)
    : IOutboundIntegrationAdapter
{
    public async Task<string> SendAsync(
        IntegrationKind kind,
        string canonicalPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var target = kind switch
        {
            IntegrationKind.LogisticsShipment => settings.LogisticsUrl,
            IntegrationKind.ElectronicInvoice => settings.EInvoiceUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported integration kind.")
        };

        if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri))
        {
            throw new IntegrationAdapterException($"No valid target URL is configured for {kind}.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
        {
            Content = new StringContent(canonicalPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Correlation-ID", correlationId);
        if (!string.IsNullOrWhiteSpace(settings.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken);
        }

        var client = httpClientFactory.CreateClient(nameof(HttpOutboundIntegrationAdapter));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Outbound {IntegrationKind} request failed with status {StatusCode}",
                kind,
                (int)response.StatusCode);
            throw new IntegrationAdapterException(
                $"Target system returned HTTP {(int)response.StatusCode} for {kind}.");
        }

        if (response.Headers.TryGetValues("X-External-Reference", out var values))
        {
            var externalReference = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(externalReference))
            {
                return externalReference;
            }
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var json = JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("id", out var id)
                    && id.ValueKind is JsonValueKind.String)
                {
                    return id.GetString()!;
                }
            }
            catch (JsonException)
            {
                logger.LogDebug("Outbound response did not contain a JSON integration identifier.");
            }
        }

        return $"HTTP-{(int)response.StatusCode}-{correlationId}";
    }
}

public sealed class IntegrationAdapterException(string message) : Exception(message);

