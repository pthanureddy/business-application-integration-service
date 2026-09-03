# Requirements traceability

This matrix connects common business-application integration requirements to concrete implementation and verification evidence.

| Requirement | Implementation | Verification |
| --- | --- | --- |
| Backend development in C#/.NET | ASP.NET Core 8 API and EF Core data layer | Release build with warnings treated as errors |
| REST APIs and JSON | Minimal API routes and typed request/response records | End-to-end API tests |
| API-first contract | Versioned OpenAPI document for every implemented route | Redocly contract validation in CI |
| Event-driven target | Versioned AsyncAPI message and Azure Service Bus queue design | AsyncAPI validation and Bicep compilation in CI; runtime is explicitly pending |
| Azure Integration Services target | API Management import and Service Bus queue with duplicate detection/dead-letter settings | Bicep compilation in CI; no deployment claim |
| Authentication | Inbound API key; outbound bearer token; SAP session cookie | Authentication and HTTP adapter tests |
| Dependency security | Current SQLite native bundle and locked NuGet graph | Vulnerability audit in CI |
| Error handling | Validation Problem Details, conflict/state/not-found handling, safe 502 integration errors | Negative-path API tests |
| Logging and traceability | Structured logging plus correlation ID middleware and propagation | API and HTTP adapter tests |
| SQL and relational concepts | SQLite through EF Core; unique composite index and persisted job state | Integration tests with isolated databases |
| Git and SDLC | Source control, pull-ready structure, automated build/test workflow | GitHub Actions |
| API testing | Importable Postman collection and local environment | Manual runbook plus automated API tests |
| Business data flows | Shipment and e-invoice canonicalization and dispatch | Mapping and successful-flow tests |
| SAP Business One/OData | Service Layer adapter, OData query builder, `B1SESSION` cookie | Focused adapter and query tests |
| Docker/cloud-ready packaging | Multi-stage Dockerfile and Compose configuration | Image configuration plus CI Docker build |
| Modernization planning | Current/target architecture, two ADRs, phased strangler roadmap, verification and rollback gates | Reviewable repository artifacts; no customer or production claim |
| Maintainability | Interfaces, options, isolated adapters, focused docs, warnings-as-errors | 25 tests; 88.67% line and 64.36% branch coverage |

The implementation does not claim access to a production ERP, vendor logistics API, or e-invoicing platform. Those dependencies are represented by configurable HTTP boundaries and controlled test doubles.
