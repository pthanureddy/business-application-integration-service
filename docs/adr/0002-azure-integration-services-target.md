# ADR 0002: Use API Management and Service Bus in the Azure target state

- Status: proposed target; infrastructure compiles, application migration pending
- Date: 2026-09-03

## Context

The implemented synchronous service demonstrates mapping and delivery behavior,
but it does not isolate intake from target outages. A modernization path also
needs governed consumer entry, versioned contracts, traffic policies, durable
messaging, and failure visibility.

## Decision

Use Azure API Management as the governed REST entry point and Azure Service Bus
as the durable command/event hand-off after a transactional outbox is added.
Keep the canonical mapping and vendor adapters inside independently testable
application boundaries. Configure duplicate detection and dead-lettering, while
requiring idempotent consumers because broker controls cannot prevent every
business duplicate.

## Alternatives considered

- **Keep synchronous point-to-point delivery:** simplest runtime, but preserves
  temporal coupling and the documented process-crash window.
- **Use only a broker:** improves decoupling but leaves API lifecycle,
  subscriptions, and consumer traffic policy fragmented.
- **Replace all established integrations together:** shortens coexistence but
  removes a safe, flow-by-flow rollback path.

## Consequences

- Intake and delivery can scale and fail independently after the outbox step.
- APIM policies and products create a shared API governance point.
- Queue backlog and dead-letter operations become production responsibilities.
- The platform introduces Azure cost, identity, network, and operational skills.
- The current Bicep template is only a target blueprint; deployment validation,
  outbox implementation, worker implementation, and recovery testing remain.
