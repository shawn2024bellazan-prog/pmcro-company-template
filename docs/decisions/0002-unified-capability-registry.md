# ADR 0002: Unified capability registry over marketplace replacement

- Status: Accepted as the architecture direction; live integrations pending.
- Decision: Maintain a normalized PMCR-O capability registry and source adapters while preserving upstream marketplaces as independent distribution systems.
- Reason: Avoid duplicating existing marketplaces, preserve provenance and licenses, and keep governance consistent across skills, MCP tools, A2A agents, plugins, scripts, and services.
- Consequences: A capability is not executable merely because it is discovered. Validation, digest/version pinning, policy approval, adapter availability, and invocation-time authorization are separate checks. Catalog-specific authentication and terms remain source-specific.
- Security: Never embed credentials in manifests. External execution and installation require explicit policy authorization and appropriate sandboxing.
