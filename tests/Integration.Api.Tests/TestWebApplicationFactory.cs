using System.Net;
using Integration.Api.Adapters;
using Integration.Api.Contracts;
using Integration.Api.Domain;
using Integration.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Integration.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public TestWebApplicationFactory()
    {
        connection.Open();
    }

    public TestOutboundIntegrationAdapter OutboundAdapter { get; } = new();

    public TestSapSalesOrderClient SapClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["IntegrationSecurity:ApiKey"] = "test-integration-key",
                    ["ConnectionStrings:IntegrationDatabase"] = "Data Source=:memory:",
                    ["Outbound:UseHttp"] = "false",
                    ["SapServiceLayer:UseHttp"] = "false"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IntegrationDbContext>>();
            services.RemoveAll<IntegrationDbContext>();
            services.AddDbContext<IntegrationDbContext>(
                options => options.UseSqlite(connection));

            services.RemoveAll<IOutboundIntegrationAdapter>();
            services.AddSingleton<IOutboundIntegrationAdapter>(OutboundAdapter);

            services.RemoveAll<ISapSalesOrderClient>();
            services.AddSingleton<ISapSalesOrderClient>(SapClient);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}

public sealed class TestOutboundIntegrationAdapter : IOutboundIntegrationAdapter
{
    private int callCount;

    public int CallCount => callCount;

    public int FailuresRemaining { get; set; }

    public List<(IntegrationKind Kind, string Payload, string CorrelationId)> Calls { get; } = [];

    public Task<string> SendAsync(
        IntegrationKind kind,
        string canonicalPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref callCount);
        Calls.Add((kind, canonicalPayload, correlationId));

        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            throw new IntegrationAdapterException("Synthetic provider timeout.");
        }

        return Task.FromResult($"TEST-{kind}-{CallCount}");
    }
}

public sealed class TestSapSalesOrderClient : ISapSalesOrderClient
{
    public DateTimeOffset? LastUpdatedSince { get; private set; }

    public Task<IReadOnlyList<SapSalesOrderResponse>> FetchUpdatedAsync(
        DateTimeOffset updatedSinceUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastUpdatedSince = updatedSinceUtc;
        IReadOnlyList<SapSalesOrderResponse> result =
        [
            new SapSalesOrderResponse(
                100,
                200,
                "C-TEST",
                2500m,
                "EUR",
                DateTimeOffset.Parse("2026-07-27T08:00:00Z"))
        ];
        return Task.FromResult(result);
    }
}

