# Capability package contract (design target)

A capability package should declare unique ID, version, publisher/source, license, supported clients, dependencies, tools, permissions, integrity hashes, validation, and rollback.

Lifecycle: discover → inspect → resolve → verify provenance/integrity → policy-check → approve if required → install isolated → validate → activate → monitor → disable/rollback/remove.

Catalog discovery, publisher trust, compatibility, and PMCR-O authorization are separate decisions. Do not execute scripts during discovery. Portable skills/MCP metadata does not mean every client supports the same hooks or agent format.
