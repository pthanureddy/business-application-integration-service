using Azure.Monitor.OpenTelemetry.AspNetCore;
using Integration.Api.Observability;
using Integration.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Integration.Api.Infrastructure;

public static class ServiceConfiguration
{
    public static IServiceCollection AddIntegrationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"]?.Trim();
        provider = string.IsNullOrWhiteSpace(provider) ? "Sqlite" : provider;

        var connectionString = configuration.GetConnectionString("IntegrationDatabase");

        return provider.ToUpperInvariant() switch
        {
            "SQLITE" => services.AddDbContext<IntegrationDbContext>(
                options => options.UseSqlite(
                    connectionString ?? "Data Source=integration.db")),
            "SQLSERVER" => services.AddDbContext<IntegrationDbContext>(
                options => options.UseSqlServer(
                    connectionString
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings:IntegrationDatabase is required for SQL Server."),
                    sqlServer => sqlServer.EnableRetryOnFailure(3))),
            _ => throw new InvalidOperationException(
                $"Unsupported DatabaseProvider '{provider}'. Use Sqlite or SqlServer.")
        };
    }

    public static IServiceCollection AddIntegrationObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"];
        serviceName = string.IsNullOrWhiteSpace(serviceName)
            ? IntegrationTelemetry.ServiceName
            : serviceName.Trim();

        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing.AddSource(IntegrationTelemetry.ActivitySourceName));

        if (!string.IsNullOrWhiteSpace(
                configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            telemetry.UseAzureMonitor();
        }

        return services;
    }
}
