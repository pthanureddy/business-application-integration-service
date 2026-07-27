using System.Security.Cryptography;
using System.Text;
using Integration.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Integration.Api.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next, IOptions<IntegrationSecurityOptions> options)
{
    public const string HeaderName = "X-Integration-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!Matches(supplied, options.Value.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Integration API authentication failed",
                    Detail = $"Provide a valid {HeaderName} header."
                });
            return;
        }

        await next(context);
    }

    private static bool Matches(string? supplied, string configured)
    {
        if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        return suppliedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}

