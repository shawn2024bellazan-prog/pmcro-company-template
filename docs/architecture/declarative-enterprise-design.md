# Declarative, template-driven enterprise design

## Objective

PMCR-O should be a generic platform rather than a collection of one-off implementations. Agents, workflows, Trail products, capability packs, and enterprise environments are described through versioned manifests and reusable templates. A shared runtime resolves those declarations into executable plans using registered adapters.

## Control plane and data plane

- **Control plane:** manifest registry, template resolver, schema/version validation, capability registry, policy evaluation, tenancy, approvals, release channels, and configuration provenance.
- **Execution plane:** orchestration loop, agent framework adapter, workflow runtime, tool/MCP adapters, queue workers, and product-specific handlers.
- **Evidence plane:** shared Trail Frame contract, protected artifact references, integrity metadata, query/replay/export services, retention, and audit access.
- **Enterprise plane:** organization/project boundaries, identity mapping, policy packs, environment overlays, observability, billing/usage hooks, backup and recovery controls.

These are logical boundaries; they need not be separate deployables in the first increment. Split into services only when reliability, scale, security, or ownership justifies it.

## Resolution pipeline

1. Accept a manifest as untrusted data; preserve the submitted source and digest.
2. Validate syntax, schema, kind-specific schema, version compatibility, and required fields.
3. Resolve `extends` using pinned versions; reject cycles and unresolved references.
4. Apply environment overlays and parameter defaults, with explicit precedence and a fully resolved output.
5. Resolve capability URIs through an allowlisted registry; never execute a manifest-provided shell command merely because it appears in YAML.
6. Evaluate policy against tenant, actor, requested capability, action risk, data classification, environment, and approval state.
7. Emit a preview/diff for high-risk changes and require approval where policy says so.
8. Execute via a registered adapter; enforce the same policy at the action boundary, not only during planning.
9. Independently verify acceptance criteria and evidence; emit Trail Frames with manifest/template/policy/runtime digests.
10. Publish a new version only after validation, security checks, tests, and provenance capture. Roll back by selecting a known-good version, not mutating history.

## Generic manifest envelope

Every resource uses `apiVersion`, `kind`, `metadata`, and `spec`, with optional `extends`, `parameters`, and `policyRefs`. The shared envelope is intentionally small. Each `kind` must have its own strict schema and semantic validator. Unknown fields should fail validation for security-sensitive kinds instead of silently changing behavior.

## Template inheritance and overlays

- Templates carry immutable versions and content digests.
- An instance pins a template version; floating tags are allowed only for development.
- Inheritance is deterministic and cycle-checked.
- Overlay order is explicit: base template → product template → tenant profile → environment overlay → approved runtime parameters.
- Policy is evaluated after resolution and remains authoritative. Child manifests may request a capability but cannot grant permissions or lower required controls.
- Secret values are supplied at runtime through secret references and are excluded from rendered manifests, logs, and Trails.

## Enterprise requirements

- Tenant identity is mandatory on every durable object and Trail record.
- Authorization is least-privilege and evaluated server-side for every sensitive action.
- Audit records include actor, correlation, policy decision, evidence references, configuration digests, and outcome.
- Sensitive payloads are stored separately under access controls, retention, and deletion policies; Trails retain only minimum necessary metadata and protected references.
- Integrity digests detect changes but do not, by themselves, prove immutability. Production ledger hardening needs signed checkpoints, key management, access separation, backup, and independent verification.
- Retention, data residency, encryption, legal hold, and export controls are configurable by policy and validated against organizational requirements.
- Fail closed for unknown kinds, versions, capabilities, policy outcomes, and high-risk actions.

## Productization

A Trail product is a versioned manifest plus contract, registered capability implementation, policies, tests, docs, telemetry, and release metadata. It is not just a query over raw frames. Product templates keep the shared core consistent while permitting domain-specific schemas and behavior.

## Incremental implementation order

1. Shared envelope/schema and sample manifests.
2. Strict parser and kind-specific validators.
3. Version-pinned template resolver with cycle detection and deterministic overlays.
4. Capability registry and policy evaluation at runtime boundaries.
5. Resolved-manifest digest in Trail Frames and CI contract tests.
6. Tenant-aware persistence and identity integration.
7. Production ledger, retention, backup/restore, and deployment adapters.

The current starter contains the declarative contract examples and design boundary. It does not yet claim a complete resolver, distributed registry, enterprise identity integration, or production-grade ledger.
