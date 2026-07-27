using System.Net;
using System.Text;
using Integration.Api.Adapters;
using Integration.Api.Configuration;
using Integration.Api.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Integration.Api.Tests;

public sealed class HttpAdapterTests
{
    [Fact]
    public async Task OutboundAdapterSendsBearerTokenCorrelationAndJson()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Headers =
                {
                    { "X-External-Reference", "LOG-9001" }
                }
            });
        var adapter = new HttpOutboundIntegrationAdapter(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(
                new OutboundOptions
                {
                    LogisticsUrl = "https://logistics.example.test/api/shipments",
                    EInvoiceUrl = "https://billing.example.test/api/invoices",
                    BearerToken = "outbound-token"
                }),
            NullLogger<HttpOutboundIntegrationAdapter>.Instance);

        var externalReference = await adapter.SendAsync(
            IntegrationKind.LogisticsShipment,
            """{"shipmentId":"SHP-1"}""",
            "corr-http-1",
            CancellationToken.None);

        Assert.Equal("LOG-9001", externalReference);
        Assert.Equal(
            new Uri("https://logistics.example.test/api/shipments"),
            handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("outbound-token", handler.AuthorizationParameter);
        Assert.Equal("corr-http-1", handler.CorrelationId);
        Assert.Equal("""{"shipmentId":"SHP-1"}""", handler.Body);
        Assert.Equal("application/json", handler.ContentType);
    }

    [Fact]
    public async Task SapClientUsesSessionCookieODataQueryAndMapsResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "value": [
                    {
                      "DocEntry": 10,
                      "DocNum": 20,
                      "CardCode": "C-10",
                      "DocTotal": 1250.50,
                      "DocCurrency": "EUR",
                      "UpdateDate": "2026-07-27T08:00:00Z"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        var handler = new RecordingHandler(response);
        var client = new HttpSapSalesOrderClient(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(
                new SapServiceLayerOptions
                {
                    BaseUrl = "https://sap.example.test",
                    SessionToken = "sap-session"
                }));

        var result = await client.FetchUpdatedAsync(
            DateTimeOffset.Parse("2026-07-26T08:00:00Z"),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(20, result[0].DocNum);
        Assert.Equal("C-10", result[0].CardCode);
        Assert.Equal("B1SESSION=sap-session", handler.Cookie);
        Assert.NotNull(handler.RequestUri);
        var decoded = Uri.UnescapeDataString(handler.RequestUri!.Query);
        Assert.Contains("$filter=UpdateDate ge '2026-07-26T08:00:00Z'", decoded);
        Assert.Contains("$select=DocEntry,DocNum,CardCode,DocTotal,DocCurrency,UpdateDate", decoded);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? CorrelationId { get; private set; }

        public string? Cookie { get; private set; }

        public string? Body { get; private set; }

        public string? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            CorrelationId = request.Headers.TryGetValues("X-Correlation-ID", out var correlations)
                ? correlations.Single()
                : null;
            Cookie = request.Headers.TryGetValues("Cookie", out var cookies)
                ? cookies.Single()
                : null;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
                ContentType = request.Content.Headers.ContentType?.MediaType;
            }

            return response;
        }
    }
}

