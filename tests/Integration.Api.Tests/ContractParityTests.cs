using YamlDotNet.RepresentationModel;

namespace Integration.Api.Tests;

public sealed class ContractParityTests
{
    [Fact]
    public void OpenApiContractMatchesImplementedRoutesAndDtoFields()
    {
        var document = LoadYaml("contracts", "openapi.yaml");
        var paths = Mapping(document, "paths");

        Assert.Equal(
            [
                "/api/v1/e-invoices",
                "/api/v1/jobs/{jobId}",
                "/api/v1/jobs/{jobId}/retry",
                "/api/v1/logistics/shipments",
                "/api/v1/sap-business-one/sales-orders",
                "/health"
            ],
            Keys(paths).Order(StringComparer.Ordinal));

        AssertSubmissionResponses(paths, "/api/v1/logistics/shipments");
        AssertSubmissionResponses(paths, "/api/v1/e-invoices");

        var schemas = Mapping(Mapping(document, "components"), "schemas");
        AssertSchema(
            schemas,
            "ShipmentRequest",
            [
                "carrierCode",
                "destinationCountry",
                "occurredAtUtc",
                "orderReference",
                "shipmentId",
                "sourceSystem",
                "status",
                "trackingNumber"
            ],
            [
                "carrierCode",
                "destinationCountry",
                "occurredAtUtc",
                "orderReference",
                "shipmentId",
                "sourceSystem",
                "status"
            ]);
        AssertSchema(
            schemas,
            "ElectronicInvoiceRequest",
            [
                "currency",
                "customerCode",
                "invoiceNumber",
                "issuedOn",
                "lines",
                "sourceSystem",
                "totalAmount"
            ],
            [
                "currency",
                "customerCode",
                "invoiceNumber",
                "issuedOn",
                "lines",
                "sourceSystem",
                "totalAmount"
            ]);
        AssertSchema(
            schemas,
            "SapSalesOrder",
            ["cardCode", "currency", "docEntry", "docNum", "docTotal", "updatedAtUtc"],
            ["cardCode", "currency", "docEntry", "docNum", "docTotal", "updatedAtUtc"]);
    }

    [Fact]
    public void AsyncApiContractLabelsQueueFlowAsTargetState()
    {
        var document = LoadYaml("contracts", "integration-events.asyncapi.yaml");
        var info = Mapping(document, "info");
        var description = Scalar(info, "description");
        var channel = Mapping(Mapping(document, "channels"), "integrationEvents");

        Assert.Equal("3.1.0", Scalar(document, "asyncapi"));
        Assert.Contains("Target event contract", description, StringComparison.Ordinal);
        Assert.Contains("current application still dispatches", description, StringComparison.Ordinal);
        Assert.Equal("integration-events", Scalar(channel, "address"));
    }

    private static void AssertSubmissionResponses(YamlMappingNode paths, string route)
    {
        var responses = Mapping(Mapping(Mapping(paths, route), "post"), "responses");
        Assert.Contains("200", Keys(responses));
        Assert.Contains("202", Keys(responses));
        Assert.Contains("409", Keys(responses));
        Assert.Contains("502", Keys(responses));
    }

    private static void AssertSchema(
        YamlMappingNode schemas,
        string schemaName,
        IReadOnlyCollection<string> expectedProperties,
        IReadOnlyCollection<string> expectedRequired)
    {
        var schema = Mapping(schemas, schemaName);
        var properties = Mapping(schema, "properties");
        var required = Sequence(schema, "required");

        Assert.Equal(
            expectedProperties.Order(StringComparer.Ordinal),
            Keys(properties).Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedRequired.Order(StringComparer.Ordinal),
            required.Children
                .Cast<YamlScalarNode>()
                .Select(item => item.Value!)
                .Order(StringComparer.Ordinal));
    }

    private static YamlMappingNode LoadYaml(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BusinessApplicationIntegration.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Could not locate the repository root.");
        }

        using var reader = File.OpenText(Path.Combine([directory.FullName, .. relativePath]));
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static YamlMappingNode Mapping(YamlMappingNode parent, string key)
    {
        return (YamlMappingNode)parent[new YamlScalarNode(key)];
    }

    private static YamlSequenceNode Sequence(YamlMappingNode parent, string key)
    {
        return (YamlSequenceNode)parent[new YamlScalarNode(key)];
    }

    private static string Scalar(YamlMappingNode parent, string key)
    {
        return ((YamlScalarNode)parent[new YamlScalarNode(key)]).Value!;
    }

    private static IReadOnlyCollection<string> Keys(YamlMappingNode mapping)
    {
        return mapping.Children.Keys
            .Cast<YamlScalarNode>()
            .Select(key => key.Value!)
            .ToArray();
    }
}
