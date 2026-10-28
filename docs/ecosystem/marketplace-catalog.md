# PMCR-O marketplace source catalog

`manifests/marketplace-sources.json` records identified or candidate sources
for reuse-first discovery. The catalog includes GitHub Marketplace, VS Code
Marketplace, Git/GitHub repositories, local and community `SKILL.md` catalogs,
private registries, MCP tools and servers, A2A agent directories, OpenAPI
services, NuGet, .NET templates, OCI artifacts, npm, and PyPI.

These are **source types and candidate adapters**, not evidence that a service
is connected, authenticated, approved, or executable. Every entry presently
has `adapter: not-implemented`; `executionEnabled` is false.

Required integration stages for each source:

1. Discover capabilities with a provenance and license record.
2. Verify source authenticity, maintenance, version and compatibility.
3. Apply tenant, permission, trust, safety and approval policies.
4. Register an exact version and adapter mapping, with bounded dependencies.
5. Test conformance and Trail evidence before enabling execution.
6. Retain a reversible disable/rollback path and causal lineage.

An available marketplace does not authorize installation. An installed plugin
does not authorize an invocation. A Trail Frame does not grant permission.

This catalog preserves ecosystem discussion from the supplied PMCR-O session
context as architectural **candidates**, without publishing the private chat
transcript, claiming complete session history, or asserting live connections.
