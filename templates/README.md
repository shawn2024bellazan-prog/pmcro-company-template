# Declarative template library

Templates define reusable defaults; manifests declare a concrete instance. A runtime resolves inheritance, validates schemas and references, binds approved capability implementations, evaluates policy, and records the resolved configuration and digest in Trail Frames.

## Rules

1. **Data describes; trusted runtime executes.** YAML/JSON cannot inject arbitrary code or bypass policy.
2. **Explicit inheritance.** `extends` resolves to a versioned template; cycles, missing parents, and ambiguous versions are errors.
3. **Safe parameterization.** Values are schema-validated; secrets are references to a secret provider, never inline values.
4. **Policy is not overridable by child manifests.** A child can request permissions but cannot grant them to itself.
5. **Portable contracts.** Manifests reference capability URIs and versioned input/output contracts, not hard-coded process paths.
6. **Resolved config is auditable.** Record source manifest, template chain, content digests, policy decisions, and runtime adapter version.
7. **Environment separation.** Development, staging, and production use overlays; production overlays may tighten controls, never weaken mandatory controls.
8. **Enterprise tenancy.** Organization/project scope, identity, data classification, retention, residency, and audit controls are explicit profile inputs.

Directories group examples by reusable concept. `manifests/examples` contains starter instances. The schemas are contract sources; validation must be run in CI and again at runtime before execution.
