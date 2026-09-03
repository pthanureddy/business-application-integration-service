# Operations runbook

## Health and startup

`GET /health` is intentionally unauthenticated so a container orchestrator can perform liveness checks. Startup fails when `IntegrationSecurity:ApiKey` is absent or blank. This prevents a silently unsecured deployment.

## Job investigation

1. Obtain the job identifier from the original API response or caller logs.
2. Call `GET /api/v1/jobs/{jobId}` with `X-Integration-Key`.
3. Review `status`, `attemptCount`, `correlationId`, `externalReference`, and `lastError`.
4. Correlate application logs using the correlation ID.
5. Resolve configuration or downstream availability before retrying.
6. Call `POST /api/v1/jobs/{jobId}/retry` only when the status is `Failed`.

Completed jobs reject retry requests to reduce accidental duplicate delivery.

## Common responses

| Status | Meaning | Operator action |
| --- | --- | --- |
| `401` | Missing or invalid inbound API key | Check secret injection and header |
| `400` | Invalid payload, timestamp, or missing idempotency key | Correct the caller request |
| `404` | Job identifier not found | Confirm environment and identifier |
| `409` | Idempotency conflict or invalid retry state | Inspect the existing job and caller key reuse |
| `502` | Downstream adapter rejected or failed delivery | Check target availability/configuration, then retry |

## Configuration safety

- Inject secrets through the deployment environment; do not add them to `appsettings.json`.
- Rotate API, bearer, and SAP session tokens according to organizational policy.
- Restrict outbound targets with network controls and TLS validation.
- Back up the database volume before upgrades.
- Retain structured logs and job records according to data-classification and privacy requirements.

## Azure monitoring and tracing

Set `APPLICATIONINSIGHTS_CONNECTION_STRING` at runtime to enable Azure Monitor
export. Use `OTEL_SERVICE_NAME` when an environment-specific service identity is
needed. Application Insights can then correlate inbound ASP.NET Core requests,
HTTP dependencies, structured logs, and the custom `integration.submit` and
`integration.dispatch` spans.

For a failed delivery:

1. Filter Application Insights by the correlation ID or integration job ID.
2. Inspect the dispatch span and its downstream HTTP dependency.
3. Compare the trace with the persisted attempt count and safe error.
4. Correct the downstream issue before using the controlled retry endpoint.

## Production evolution

Before high-volume production use:

- replace `EnsureCreated` with versioned EF Core migrations;
- apply versioned migrations and capacity-test SQL Server for multi-instance deployment;
- use a transactional outbox and queue-backed worker;
- add retry backoff, jitter, dead-letter review, and replay authorization;
- define environment-specific Azure Monitor alerts and service-level objectives;
- add rate limiting, workload identity, and a managed secrets store;
- perform vendor contract tests and load/failure testing.
