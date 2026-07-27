using System.Text.Json.Serialization;
using Integration.Api.Adapters;
using Integration.Api.Configuration;
using Integration.Api.Endpoints;
using Integration.Api.Middleware;
using Integration.Api.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOptions<IntegrationSecurityOptions>()
    .Bind(builder.Configuration.GetSection(IntegrationSecurityOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "Integration API key is required.")
    .ValidateOnStart();
builder.Services.Configure<OutboundOptions>(
    builder.Configuration.GetSection(OutboundOptions.SectionName));
builder.Services.Configure<SapServiceLayerOptions>(
    builder.Configuration.GetSection(SapServiceLayerOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("IntegrationDatabase")
    ?? "Data Source=integration.db";
builder.Services.AddDbContext<IntegrationDbContext>(
    options => options.UseSqlite(connectionString));
builder.Services.AddScoped<Integration.Api.Services.IntegrationOrchestrator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient();

if (builder.Configuration.GetValue<bool>("Outbound:UseHttp"))
{
    builder.Services.AddTransient<IOutboundIntegrationAdapter, HttpOutboundIntegrationAdapter>();
}
else
{
    builder.Services.AddSingleton<IOutboundIntegrationAdapter, SimulatedOutboundIntegrationAdapter>();
}

if (builder.Configuration.GetValue<bool>("SapServiceLayer:UseHttp"))
{
    builder.Services.AddTransient<ISapSalesOrderClient, HttpSapSalesOrderClient>();
}
else
{
    builder.Services.AddSingleton<ISapSalesOrderClient, SimulatedSapSalesOrderClient>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "UP",
    checkedAtUtc = DateTimeOffset.UtcNow
}));
app.MapIntegrationEndpoints();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
    database.Database.EnsureCreated();
}

app.Run();

public partial class Program
{
}
