using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Integration.Api.Contracts;
using Integration.Api.Domain;

namespace Integration.Api.Tests;

public sealed class AccountsPayableWorkflowTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task KnownSupplierReceivesCodingProposalAndApprovalRoute()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-100", "AP-1001", 12_500m, "PO-9001", "5400", "LOG-01"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<AccountsPayableInvoiceResponse>(JsonOptions);
        Assert.NotNull(invoice);
        Assert.Equal(AccountsPayableInvoiceStatus.AwaitingApproval, invoice.Status);
        Assert.Equal("5400", invoice.SuggestedAccount);
        Assert.Equal("LOG-01", invoice.SuggestedCostCenter);
        Assert.Equal("CostCenterOwner", invoice.RequiredApprovalRole);
        Assert.True(invoice.TouchlessCandidate);
        Assert.Empty(invoice.ExceptionCodes);
    }

    [Fact]
    public async Task UnknownSupplierIsRoutedToExceptionQueue()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-999", "AP-1002", 900m, "PO-9002"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<AccountsPayableInvoiceResponse>(JsonOptions);
        Assert.NotNull(invoice);
        Assert.Equal(AccountsPayableInvoiceStatus.Exception, invoice.Status);
        Assert.Contains("SUPPLIER_NOT_FOUND_OR_INACTIVE", invoice.ExceptionCodes);
        Assert.False(invoice.TouchlessCandidate);

        var queue = await client.GetFromJsonAsync<List<AccountsPayableInvoiceResponse>>(
            "/api/v1/accounts-payable/exceptions",
            JsonOptions);
        Assert.NotNull(queue);
        Assert.Single(queue);
        Assert.Equal(invoice.Id, queue[0].Id);
    }

    [Fact]
    public async Task MissingPurchaseOrderAndCodingDeviationAreVisible()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-200", "AP-1003", 30_000m, null, "9999", "OTHER"));
        var invoice = await response.Content.ReadFromJsonAsync<AccountsPayableInvoiceResponse>(JsonOptions);

        Assert.NotNull(invoice);
        Assert.Equal(AccountsPayableInvoiceStatus.Exception, invoice.Status);
        Assert.Equal("FinanceManager", invoice.RequiredApprovalRole);
        Assert.Equal(
            ["ACCOUNT_DEVIATION", "COST_CENTER_DEVIATION", "PURCHASE_ORDER_REFERENCE_MISSING"],
            invoice.ExceptionCodes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NaturalInvoiceKeyRejectsDuplicates()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var request = Invoice("SUP-100", "AP-1004", 2_500m, "PO-9004");

        var first = await client.PostAsJsonAsync("/api/v1/accounts-payable/invoices", request);
        var duplicate = await client.PostAsJsonAsync("/api/v1/accounts-payable/invoices", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Duplicate supplier invoice", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ApprovalMustMatchAmountBasedRoutingRule()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var submitted = await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-100", "AP-1005", 150_000m, "PO-9005"));
        var invoice = await submitted.Content.ReadFromJsonAsync<AccountsPayableInvoiceResponse>(JsonOptions);
        Assert.NotNull(invoice);
        Assert.Equal("FinanceManagerAndCfo", invoice.RequiredApprovalRole);

        var wrongRole = await client.PostAsJsonAsync(
            $"/api/v1/accounts-payable/invoices/{invoice.Id}/approve",
            new AccountsPayableDecisionRequest("FinanceManager", "finance.manager@example.invalid"));
        Assert.Equal(HttpStatusCode.Conflict, wrongRole.StatusCode);

        var approved = await client.PostAsJsonAsync(
            $"/api/v1/accounts-payable/invoices/{invoice.Id}/approve",
            new AccountsPayableDecisionRequest(
                "FinanceManagerAndCfo",
                "dual.approval@example.invalid"));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedInvoice = await approved.Content.ReadFromJsonAsync<AccountsPayableInvoiceResponse>(JsonOptions);
        Assert.NotNull(approvedInvoice);
        Assert.Equal(AccountsPayableInvoiceStatus.ReadyForPayment, approvedInvoice.Status);
        Assert.Equal("dual.approval@example.invalid", approvedInvoice.DecisionBy);
    }

    [Fact]
    public async Task AnalyticsExposeExceptionAndAutomationRates()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-100", "AP-1006", 1_000m, "PO-9006"));
        await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-999", "AP-1007", 1_000m, "PO-9007"));

        var analytics = await client.GetFromJsonAsync<AccountsPayableAnalyticsResponse>(
            "/api/v1/accounts-payable/analytics");

        Assert.NotNull(analytics);
        Assert.Equal(2, analytics.TotalInvoices);
        Assert.Equal(1, analytics.ExceptionInvoices);
        Assert.Equal(1, analytics.AwaitingApprovalInvoices);
        Assert.Equal(1, analytics.TouchlessCandidates);
        Assert.Equal(50m, analytics.TouchlessCandidateRatePercent);
    }

    [Fact]
    public async Task DueDateBeforeInvoiceDateReturnsValidationProblem()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var request = Invoice("SUP-100", "AP-1008", 1_000m, "PO-9008") with
        {
            DueOn = new DateOnly(2026, 10, 31)
        };

        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("DueOn", out _));
    }

    [Fact]
    public async Task ExceptionInvoiceCannotBeApproved()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateAuthenticatedClient(factory);
        var submitted = await client.PostAsJsonAsync(
            "/api/v1/accounts-payable/invoices",
            Invoice("SUP-999", "AP-1009", 1_000m, "PO-9009"));
        var invoice = await submitted.Content.ReadFromJsonAsync<AccountsPayableInvoiceResponse>(JsonOptions);
        Assert.NotNull(invoice);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/accounts-payable/invoices/{invoice.Id}/approve",
            new AccountsPayableDecisionRequest("CostCenterOwner", "owner@example.invalid"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid accounts-payable invoice state", problem.GetProperty("title").GetString());
    }

    private static HttpClient CreateAuthenticatedClient(TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Integration-Key", "test-integration-key");
        return client;
    }

    private static AccountsPayableInvoiceRequest Invoice(
        string supplierNumber,
        string invoiceNumber,
        decimal grossAmount,
        string? purchaseOrderReference,
        string? account = null,
        string? costCenter = null)
    {
        return new AccountsPayableInvoiceRequest(
            supplierNumber,
            invoiceNumber,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 12, 1),
            "SEK",
            grossAmount,
            purchaseOrderReference,
            account,
            costCenter);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
