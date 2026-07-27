using Integration.Api.Adapters;
using Integration.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Integration.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            IntegrationJobNotFoundException => (StatusCodes.Status404NotFound, "Integration job not found"),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency conflict"),
            IntegrationJobStateException => (StatusCodes.Status409Conflict, "Invalid integration job state"),
            ExternalIntegrationException => (StatusCodes.Status502BadGateway, "Outbound integration failed"),
            IntegrationAdapterException => (StatusCodes.Status502BadGateway, "Business application request failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Request failed with status {StatusCode}", status);
        }
        else
        {
            logger.LogInformation(exception, "Request rejected with status {StatusCode}", status);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "The request could not be completed."
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is ExternalIntegrationException external)
        {
            problem.Extensions["jobId"] = external.JobId;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}

