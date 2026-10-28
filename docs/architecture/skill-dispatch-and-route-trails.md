# Skill dispatch and route-resolution Trail Frames

`SkillRoutedCommandHandler<TPayload,TResult>` adapts a normalized PMCR-O command to a declarative skill route. It is registered like any other `IPmcroCommandHandler`, so transport-specific clients still converge on the application router.

## Order of operations

1. The application router validates the command envelope and authorizes the command.
2. The skill-routed handler resolves the exact command type against the route catalog.
3. It records a `route-resolution` Enterprise Trail Frame with intent/run/correlation lineage, route/capability/skill identifiers, policy reference, required permission names, and decision code.
4. Missing, disabled, ambiguous, invalid, or unavailable routes are blocked without calling the dispatcher.
5. For a matched route, the dispatcher must independently enforce the referenced policy, actor and tenant scope, permission set, version constraints, input schema, and execution sandbox before invoking the skill.
6. Route identity is added to the result evidence; the outer command router records the final command outcome.

## Security boundary

A route is configuration, not an authorization grant. `PolicyRef` and `RequiredPermissions` are evidence inputs for the runtime policy engine; they must not be trusted merely because the catalog contains them. Catalog writes need authenticated, authorized, audited administration. The JSON catalog and JSONL Trail store are single-process reference implementations and are not distributed production stores.

Trail evidence excludes command payloads by default. Exception messages are not exposed; only exception type is recorded. Even so, evidence metadata should be reviewed for sensitive identifiers before production use.

## Conformance checks

- Missing/disabled/ambiguous routes fail closed.
- A blocked route never calls the dispatcher.
- A successful resolution emits a Trail Frame with matching run and correlation IDs.
- Downstream adapters preserve cancellation and command lineage.
- Runtime policy and permissions are checked before external side effects.
- Integration tests should cover tenant isolation, stale versions, permission denial, timeout, retry/idempotency, secret redaction, and dispatcher side-effect uncertainty.
