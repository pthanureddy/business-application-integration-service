using System.Text.Json;
using Integration.Api.Configuration;
using Integration.Api.Contracts;
using Microsoft.Extensions.Options;

namespace Integration.Api.Adapters;

public sealed class HttpSapSalesOrderClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SapServiceLayerOptions> options)
    : ISapSalesOrderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<SapSalesOrderResponse>> FetchUpdatedAsync(
        DateTimeOffset updatedSinceUtc,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new IntegrationAdapterException("SAP Business One Service Layer base URL is invalid.");
        }

        if (string.IsNullOrWhiteSpace(settings.SessionToken))
        {
            throw new IntegrationAdapterException("SAP Business One Service Layer session token is not configured.");
        }

        var requestUri = new Uri(baseUri, ODataQueryBuilder.BuildUpdatedSalesOrdersPath(updatedSinceUtc));
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("Cookie", $"B1SESSION={settings.SessionToken}");
        request.Headers.Accept.ParseAdd("application/json");

        var client = httpClientFactory.CreateClient(nameof(HttpSapSalesOrderClient));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new IntegrationAdapterException(
                $"SAP Business One Service Layer returned HTTP {(int)response.StatusCode}.");
        }

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<ODataEnvelope<SapOrderRecord>>(
            content,
            JsonOptions,
            cancellationToken);

        return result?.Value.Select(Map).ToList() ?? [];
    }

    private static SapSalesOrderResponse Map(SapOrderRecord order)
    {
        return new SapSalesOrderResponse(
            order.DocEntry,
            order.DocNum,
            order.CardCode,
            order.DocTotal,
            order.DocCurrency,
            order.UpdateDate);
    }

    private sealed record ODataEnvelope<T>(IReadOnlyList<T> Value);

    private sealed record SapOrderRecord(
        int DocEntry,
        int DocNum,
        string CardCode,
        decimal DocTotal,
        string DocCurrency,
        DateTimeOffset UpdateDate);
}
