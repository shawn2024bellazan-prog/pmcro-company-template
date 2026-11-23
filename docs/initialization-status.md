# Initialization status and acceptance boundary

- **SOURCE_BOOTSTRAP:** Bundled plugin source and root agent contract added to a review branch. Compare against upstream pinned commit.
- **PROFILE_INITIALIZATION:** `NOT_INVOKED`. The separately installed `pmcro-commands:init` requires its complete authoritative source dependency tree. It has not been executed in this environment.
- **Native .NET/MAF/MCP/A2A:** `NOT_INVOKED`.
- **Checker disposition:** no runtime PASS can be claimed without an actual command invocation, verified return, and a hash-sealed trail receipt.

Next action in an authorized local project: resolve a complete `pmcro-commands:init` installation and run its `write --root PROJECT` then `verify --root PROJECT`. Capture their actual stdout and exit codes. Keep generated `.pmcro/` out of authored source.
