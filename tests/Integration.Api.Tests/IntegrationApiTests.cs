using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Integration.Api.Contracts;

namespace Integration.Api.Tests;

public sealed class IntegrationApiTests
{
    private static readonly JsonSerializerOptions JobJsonOptions = CreateJobJsonOptions();

    [Fact]
    public async Task HealthEndpointDoesNotRequireAuthentication()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UP", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task IntegrationEndpointsRequireApiKey()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/jobs/22a7b13a-fb13-4ce4-9d4d-0bd25ea08457");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShipmentSubmissionIsPersistedAndIdempotent()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var first = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("IN_TRANSIT"),
            "shipment-1001",
            "correlation-1001");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<IntegrationJobResponse>(JobJsonOptions);
        Assert.NotNull(firstBody);
        Assert.False(firstBody.Duplicate);
        Assert.Equal("Succeeded", firstBody.Status.ToString());
        Assert.Equal(1, factory.OutboundAdapter.CallCount);

        var duplicate = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("IN_TRANSIT"),
            "shipment-1001",
            "correlation-1001");

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<IntegrationJobResponse>(JobJsonOptions);
        Assert.NotNull(duplicateBody);
        Assert.True(duplicateBody.Duplicate);
        Assert.Equal(firstBody.Id, duplicateBody.Id);
        Assert.Equal(1, factory.OutboundAdapter.CallCount);

        var stored = await client.GetFromJsonAsync<IntegrationJobResponse>(
            $"/api/v1/jobs/{firstBody.Id}",
            JobJsonOptions);
        Assert.NotNull(stored);
        Assert.Equal(firstBody.Id, stored.Id);
        Assert.Equal(1, stored.AttemptCount);
    }

    [Fact]
    public async Task ReusedIdempotencyKeyWithDifferentPayloadReturnsConflict()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("IN_TRANSIT"),
            "shipment-conflict",
            "correlation-a");

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("DELIVERED"),
            "shipment-conflict",
            "correlation-b");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Idempotency conflict", body.GetProperty("title").GetString());
        Assert.Equal(1, factory.OutboundAdapter.CallCount);
    }

    [Fact]
    public async Task MissingIdempotencyKeyReturnsValidationProblem()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/logistics/shipments")
        {
            Content = JsonContent.Create(Shipment("IN_TRANSIT"))
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
    }

    [Fact]
    public async Task InvoiceTotalMismatchReturnsFieldValidationError()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var invoice = new ElectronicInvoiceRequest(
            "billing-platform",
            "INV-2001",
            "C-100",
            "EUR",
            new DateOnly(2026, 7, 27),
            999m,
            [new ElectronicInvoiceLine("CHEM-01", 2, 100m, 25m)]);

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/e-invoices",
            invoice,
            "invoice-invalid",
            "invoice-correlation");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("TotalAmount", out _));
        Assert.Equal(0, factory.OutboundAdapter.CallCount);
    }

    [Fact]
    public async Task ValidInvoiceIsMappedAndDispatched()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var invoice = new ElectronicInvoiceRequest(
            "billing-platform",
            "INV-2002",
            "C-200",
            "eur",
            new DateOnly(2026, 7, 27),
            250m,
            [new ElectronicInvoiceLine("CHEM-02", 2, 100m, 25m)]);

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/e-invoices",
            invoice,
            "invoice-valid",
            "invoice-valid-correlation");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<IntegrationJobResponse>(JobJsonOptions);
        Assert.NotNull(job);
        Assert.Equal("Succeeded", job.Status.ToString());
        Assert.Single(factory.OutboundAdapter.Calls);
        Assert.Equal("ElectronicInvoice", factory.OutboundAdapter.Calls[0].Kind.ToString());
        using var payload = JsonDocument.Parse(factory.OutboundAdapter.Calls[0].Payload);
        Assert.Equal("EUR", payload.RootElement.GetProperty("currency").GetString());
        Assert.Equal(250m, payload.RootElement.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task ProviderFailureRetainsFailedJobForInvestigation()
    {
        using var factory = new TestWebApplicationFactory();
        factory.OutboundAdapter.FailuresRemaining = 1;
        using var client = CreateAuthenticatedClient(factory);

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("IN_TRANSIT"),
            "shipment-failure",
            "failure-correlation");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = problem.GetProperty("jobId").GetGuid();

        var job = await client.GetFromJsonAsync<IntegrationJobResponse>(
            $"/api/v1/jobs/{jobId}",
            JobJsonOptions);
        Assert.NotNull(job);
        Assert.Equal("Failed", job.Status.ToString());
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal("Synthetic provider timeout.", job.LastError);
    }

    [Fact]
    public async Task FailedJobCanBeRetriedWithoutCreatingAnotherRecord()
    {
        using var factory = new TestWebApplicationFactory();
        factory.OutboundAdapter.FailuresRemaining = 1;
        using var client = CreateAuthenticatedClient(factory);
        var failed = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("IN_TRANSIT"),
            "shipment-retry",
            "retry-correlation");
        var problem = await failed.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = problem.GetProperty("jobId").GetGuid();

        var retry = await client.PostAsync($"/api/v1/jobs/{jobId}/retry", content: null);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var job = await retry.Content.ReadFromJsonAsync<IntegrationJobResponse>(JobJsonOptions);
        Assert.NotNull(job);
        Assert.Equal("Succeeded", job.Status.ToString());
        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(2, factory.OutboundAdapter.CallCount);
    }

    [Fact]
    public async Task SapBusinessOneEndpointPassesUtcFilterToClient()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);

        var orders = await client.GetFromJsonAsync<List<SapSalesOrderResponse>>(
            "/api/v1/sap-business-one/sales-orders?updatedSince=2026-07-26T08:00:00%2B02:00");

        Assert.NotNull(orders);
        Assert.Single(orders);
        Assert.Equal(200, orders[0].DocNum);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-26T06:00:00Z"),
            factory.SapClient.LastUpdatedSince);
    }

    [Fact]
    public async Task MissingSapTimestampReturnsValidationProblem()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/api/v1/sap-business-one/sales-orders");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("updatedSince", out _));
    }

    [Fact]
    public async Task UnknownJobReturnsStructuredNotFoundProblem()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var jobId = Guid.Parse("22a7b13a-fb13-4ce4-9d4d-0bd25ea08457");

        var response = await client.GetAsync($"/api/v1/jobs/{jobId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Integration job not found", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SuccessfulJobCannotBeRetried()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var submitted = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/logistics/shipments",
            Shipment("IN_TRANSIT"),
            "shipment-complete",
            "complete-correlation");
        var job = await submitted.Content.ReadFromJsonAsync<IntegrationJobResponse>(JobJsonOptions);
        Assert.NotNull(job);

        var response = await client.PostAsync($"/api/v1/jobs/{job.Id}/retry", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid integration job state", body.GetProperty("title").GetString());
    }

    private static HttpClient CreateAuthenticatedClient(TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Integration-Key", "test-integration-key");
        return client;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpClient client,
        HttpMethod method,
        string uri,
        T payload,
        string idempotencyKey,
        string correlationId)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-ID", correlationId);
        return await client.SendAsync(request);
    }

    private static ShipmentIntegrationRequest Shipment(string status)
    {
        return new ShipmentIntegrationRequest(
            "warehouse-management",
            "SHP-1001",
            "SO-8001",
            "DHL",
            "TRACK-1001",
            status,
            DateTimeOffset.Parse("2026-07-27T08:00:00Z"),
            "ES");
    }

    private static JsonSerializerOptions CreateJobJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
