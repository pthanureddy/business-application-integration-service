using Integration.Api.Infrastructure;
using Integration.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Api.Tests;

public sealed class ServiceConfigurationTests
{
    [Fact]
    public void SqliteIsTheDefaultDatabaseProvider()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:IntegrationDatabase"] = "Data Source=integration.db"
            });

        var options = provider.GetRequiredService<DbContextOptions<IntegrationDbContext>>();

        Assert.Contains(
            options.Extensions,
            extension => extension.GetType().Name == "SqliteOptionsExtension");
    }

    [Fact]
    public void SqlServerCanBeSelectedForDeployedEnvironments()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "SqlServer",
                ["ConnectionStrings:IntegrationDatabase"] =
                    "Server=localhost;Database=Integration;User Id=sa;Password=Test-Only!123;"
            });

        var options = provider.GetRequiredService<DbContextOptions<IntegrationDbContext>>();

        Assert.Contains(
            options.Extensions,
            extension => extension.GetType().Name == "SqlServerOptionsExtension");
    }

    [Fact]
    public void UnsupportedDatabaseProviderFailsFast()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Unknown"
                })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddIntegrationPersistence(configuration));

        Assert.Contains("Use Sqlite or SqlServer", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddIntegrationPersistence(configuration);
        return services.BuildServiceProvider();
    }
}
