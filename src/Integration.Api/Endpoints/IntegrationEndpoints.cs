using Integration.Api.Adapters;
using Integration.Api.Contracts;
using Integration.Api.Domain;
using Integration.Api.Middleware;
using Integration.Api.Services;

namespace Integration.Api.Endpoints;

public static class IntegrationEndpoints
{
    private const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapPost("/logistics/shipments", SubmitShipmentAsync);
        api.MapPost("/e-invoices", SubmitInvoiceAsync);
        api.MapGet("/jobs/{jobId:guid}", GetJobAsync);
        api.MapPost("/jobs/{jobId:guid}/retry", RetryJobAsync);
        api.MapGet("/sap-business-one/sales-orders", GetSapSalesOrdersAsync);

        return endpoints;
    }

    private static async Task<IResult> SubmitShipmentAsync(
        ShipmentIntegrationRequest request,
        HttpContext context,
        IntegrationOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidation.Validate(request);
        if (!TryGetIdempotencyKey(context, errors, out var idempotencyKey))
        {
            return Results.ValidationProblem(errors);
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await orchestrator.SubmitAsync(
            IntegrationKind.LogisticsShipment,
            request.SourceSystem,
            idempotencyKey!,
            GetCorrelationId(context),
            CanonicalPayloadMapper.Map(request),
            cancellationToken);

        var response = IntegrationJobResponse.From(result.Job, result.Duplicate);
        return result.Duplicate
            ? Results.Ok(response)
            : Results.Accepted($"/api/v1/jobs/{result.Job.Id}", response);
    }

    private static async Task<IResult> SubmitInvoiceAsync(
        ElectronicInvoiceRequest request,
        HttpContext context,
        IntegrationOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidation.Validate(request);
        if (!TryGetIdempotencyKey(context, errors, out var idempotencyKey))
        {
            return Results.ValidationProblem(errors);
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await orchestrator.SubmitAsync(
            IntegrationKind.ElectronicInvoice,
            request.SourceSystem,
            idempotencyKey!,
            GetCorrelationId(context),
            CanonicalPayloadMapper.Map(request),
            cancellationToken);

        var response = IntegrationJobResponse.From(result.Job, result.Duplicate);
        return result.Duplicate
            ? Results.Ok(response)
            : Results.Accepted($"/api/v1/jobs/{result.Job.Id}", response);
    }

    private static async Task<IResult> GetJobAsync(
        Guid jobId,
        IntegrationOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var job = await orchestrator.GetAsync(jobId, cancellationToken);
        return Results.Ok(IntegrationJobResponse.From(job));
    }

    private static async Task<IResult> RetryJobAsync(
        Guid jobId,
        IntegrationOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var job = await orchestrator.RetryAsync(jobId, cancellationToken);
        return Results.Ok(IntegrationJobResponse.From(job));
    }

    private static async Task<IResult> GetSapSalesOrdersAsync(
        DateTimeOffset? updatedSince,
        ISapSalesOrderClient client,
        CancellationToken cancellationToken)
    {
        if (updatedSince is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["updatedSince"] = ["A UTC timestamp is required."]
                });
        }

        var orders = await client.FetchUpdatedAsync(updatedSince.Value.ToUniversalTime(), cancellationToken);
        return Results.Ok(orders);
    }

    private static bool TryGetIdempotencyKey(
        HttpContext context,
        IDictionary<string, string[]> errors,
        out string? idempotencyKey)
    {
        idempotencyKey = context.Request.Headers[IdempotencyHeader].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            errors[IdempotencyHeader] = ["An idempotency key is required."];
            return false;
        }

        if (idempotencyKey.Length > 160)
        {
            errors[IdempotencyHeader] = ["Idempotency key must not exceed 160 characters."];
            return false;
        }

        return true;
    }

    private static string GetCorrelationId(HttpContext context)
    {
        return (string)context.Items[CorrelationIdMiddleware.ItemName]!;
    }
}

