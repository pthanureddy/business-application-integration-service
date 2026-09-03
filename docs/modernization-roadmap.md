# Legacy integration modernization roadmap

## Starting assumption

This roadmap is for a representative estate with point-to-point HTTP/file
transfers and an established broker or BizTalk-like integration platform. The
repository has not assessed a real customer estate and does not claim BizTalk
delivery experience. Product inventory, support dates, message volumes,
licensing, data classifications, and recovery objectives must be discovered
before a real roadmap is approved.

## Outcome

Move selected flows behind governed REST contracts and, where decoupling is
needed, onto Azure Service Bus. Preserve business behavior and rollback options
while establishing traceable requirements, contract tests, telemetry, and
operational ownership.

## Phases and decision gates

### 0. Discover and baseline

Activities:

- inventory interfaces, owners, consumers, schedules, schemas, transformations,
  credentials, data sensitivity, dependencies, and failure procedures;
- capture traffic volume, payload size, latency, availability, recovery, and
  retention requirements;
- identify duplicate detection, ordering, transaction, and replay semantics;
- rank flows by business criticality, change rate, coupling, and migration risk.

Exit gate: every pilot candidate has a named owner, observed baseline, dependency
map, acceptance criteria, and rollback method. Unknown business semantics block
migration rather than becoming implementation assumptions.

### 1. Establish platform and contract guardrails

Activities:

- review the OpenAPI and AsyncAPI contracts with producer, consumer, security,
  operations, and data owners;
- compile and policy-check the APIM and Service Bus Bicep templates;
- define identity, secret, network, logging, retention, and environment controls;
- create contract, integration, failure, performance, and recovery test plans;
- define dashboards and alert thresholds from the agreed objectives.

Exit gate: contracts are versioned, infrastructure is validated in a non-production
subscription, access follows least privilege, and operational owners accept the
runbook and alert paths.

### 2. Migrate one low-risk flow with a strangler boundary

Activities:

- route one consumer through API Management while the established flow remains
  available;
- map the established payload to the canonical model at one controlled boundary;
- implement the transactional outbox and Service Bus publisher described in the
  strategy; do not claim asynchronous reliability before these exist;
- run contract and reconciliation tests against captured, de-identified cases;
- compare accepted, completed, failed, and duplicate counts between paths.

Exit gate: no unexplained reconciliation differences, failure and replay drills
pass, telemetry traces a request across each boundary, and rollback meets the
agreed recovery time.

### 3. Controlled cutover and scale-out

Activities:

- move consumers in cohorts using APIM routing or configuration flags;
- freeze breaking schema changes during each cutover window;
- monitor error rate, queue depth, oldest-message age, downstream latency, and
  dead-letter count;
- stop and roll back when a threshold is breached; retain evidence for analysis;
- repeat by integration family only after the pilot exit criteria remain stable.

Exit gate: business owners accept reconciled outputs, operations can recover and
replay safely, and the old path has no remaining unapproved consumers.

### 4. Decommission and improve

Activities:

- archive configurations, mappings, audit evidence, and support documentation
  according to retention rules;
- revoke old service accounts, firewall rules, certificates, and schedules;
- remove the established route only after an observation period and owner sign-off;
- review incidents, delivery lead time, platform cost, and contract-change data
  before prioritizing the next wave.

Exit gate: the old runtime is inactive, rollback artifacts are retained for the
approved period, ownership is transferred, and savings or quality claims use
measured rather than estimated results.

## Verification matrix

| Risk | Verification evidence | Stop/rollback trigger |
| --- | --- | --- |
| Contract drift | OpenAPI/AsyncAPI validation, consumer contract tests | Breaking change without a new version or approved consumer plan |
| Lost or duplicate work | Reconciliation by business key, outbox ID, and message ID | Unexplained missing item or non-idempotent duplicate side effect |
| Downstream outage | Failure-injection test, retry count, dead-letter inspection | Backlog age or error rate exceeds agreed objective |
| Traceability gap | Correlation search across APIM, API, job store, queue, and worker | Any migrated item cannot be traced end to end |
| Security regression | Identity/access review, secret scan, network validation | Shared credentials, excessive role, or unintended public endpoint |
| Cutover regression | Cohort comparison and business reconciliation | Material mismatch or recovery objective cannot be met |

## Roadmap limitations

Dates, cost, team size, APIM tier, region topology, capacity, and final network
design are intentionally absent. They require customer constraints and measured
workload data. The Bicep files are compile-validated target artifacts, not proof
that a cloud transformation or production migration has been delivered.
