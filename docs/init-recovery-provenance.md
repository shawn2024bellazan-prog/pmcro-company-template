# PMCR-O /init recovery provenance

The installed \`pmcro-commands:init\` handler had an unresolvable dependency on
\`config.py\`, \`profile_builder.py\`, and \`audit.py\`.

The handler at \`plugins/pmcro-commands/skills/init/scripts/init.py\` is a byte-for-byte
copy of that installed plugin skill. The three companion modules are **new**
recovery implementations: they do **not** claim to reconstruct unavailable
original source. They discover the real source skills under
\`plugins/pmcro\` and \`plugins/pmcro-orchestrator\`.

The output \`.pmcro/\` is a managed configuration projection, not an executable
MAF agent. A native PMCR-O, A2A, or MCP runtime does not run during \`/init\`.
An independent verifier \`scripts/verify_profile.py\` rechecks receipt,
manifest, and artifact SHA-256 values. CI must run on actual repository source
before claiming a repository-level proof.

The MCP proof remains a separate lane, covered by \`mcp-proof.yml\`.
