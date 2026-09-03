using System.Diagnostics;
using Integration.Api.Domain;

namespace Integration.Api.Observability;

public static class IntegrationTelemetry
{
    public const string ActivitySourceName = "Integration.Api.Orchestration";
    public const string ServiceName = "business-application-integration-service";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    public static Activity? StartSubmission(IntegrationKind kind, string sourceSystem)
    {
        var activity = Source.StartActivity("integration.submit", ActivityKind.Internal);
        activity?.SetTag("integration.kind", kind.ToString());
        activity?.SetTag("integration.source_system", sourceSystem);
        return activity;
    }

    public static Activity? StartDispatch(IntegrationJob job)
    {
        var activity = Source.StartActivity("integration.dispatch", ActivityKind.Client);
        activity?.SetTag("integration.job_id", job.Id);
        activity?.SetTag("integration.kind", job.Kind.ToString());
        activity?.SetTag("integration.attempt", job.AttemptCount + 1);
        return activity;
    }
}
