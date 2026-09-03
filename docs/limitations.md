# Scope and limitations

## Implemented and verified

- Local ASP.NET Core integration API
- SQLite and SQL Server EF Core provider paths
- Logistics shipment and electronic invoice mappings
- Idempotency, correlation, validation, errors, and failed-job retry
- Real HTTP client code exercised with deterministic in-memory HTTP handlers
- SAP Business One Service Layer OData request and response mapping
- Automated tests, Docker packaging, Postman assets, and CI configuration
- OpenTelemetry instrumentation with conditional Azure Monitor export
- Compiled Azure infrastructure template for Container Apps, SQL, Log Analytics, and Application Insights
- Validated OpenAPI contract for the implemented HTTP surface
- Validated target-state AsyncAPI contract and compiled APIM/Service Bus Bicep
- Architecture decisions and a phased modernization roadmap with verification and rollback gates

## Not claimed

- Production access to SAP Business One, Beas Manufacturing, or Produmex WMS
- Validation against a particular logistics provider or e-invoicing vendor contract
- Production operation, scale, uptime, security certification, or user adoption
- SAP session login/renewal or company-database selection
- A live deployment to Azure or another cloud environment
- Event-broker or transactional-outbox delivery guarantees
- A completed BizTalk or other legacy-platform assessment or migration
- Customer workshops, architecture governance, or a production technical roadmap

## Next steps with real stakeholders

1. Capture source/target ownership, schemas, throughput, latency, retention, and recovery objectives.
2. Confirm identity, network, and credential-rotation patterns.
3. Replace example mappings with versioned vendor contract models.
4. Add contract tests against non-production endpoints.
5. Introduce queue-backed delivery and operational dashboards.
6. Run failure-mode, load, security, and data-protection reviews before release.
