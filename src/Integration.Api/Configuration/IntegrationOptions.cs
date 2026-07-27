namespace Integration.Api.Configuration;

public sealed class IntegrationSecurityOptions
{
    public const string SectionName = "IntegrationSecurity";

    public string ApiKey { get; init; } = string.Empty;
}

public sealed class OutboundOptions
{
    public const string SectionName = "Outbound";

    public bool UseHttp { get; init; }

    public string LogisticsUrl { get; init; } = string.Empty;

    public string EInvoiceUrl { get; init; } = string.Empty;

    public string BearerToken { get; init; } = string.Empty;
}

public sealed class SapServiceLayerOptions
{
    public const string SectionName = "SapServiceLayer";

    public bool UseHttp { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string SessionToken { get; init; } = string.Empty;
}
