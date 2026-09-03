# ADR 0001: Use reviewed API and event contracts as integration boundaries

- Status: accepted for the reference architecture
- Date: 2026-09-03

## Context

The service previously exposed tested routes but had no repository-level
contract for consumer review. A future asynchronous path also needs an explicit
message boundary before a broker implementation is introduced.

## Decision

Maintain an OpenAPI contract for implemented HTTP behavior and an AsyncAPI
contract for the explicitly labelled target event. Contract validation runs in
CI. Implementations, tests, examples, and documentation must change with the
contract. Breaking semantics require a new major route or event version and a
consumer migration plan.

## Consequences

- Reviewers can assess boundaries without reading framework code.
- API Management can import the same OpenAPI artifact used in review.
- The target event design is visible before the outbox/publisher is built.
- Contract review adds work to every interface change.
- AsyncAPI presence alone does not prove that asynchronous delivery is
  implemented; repository wording must preserve that distinction.
