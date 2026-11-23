# PMCR-O initialization and execution status

**Verified on GitHub Actions 2026-11-23:**

- **Authoring source:** seven existing skill definitions imported from pinned upstream commit `9093b91df2527a7fbb0d390e90b8d04775077587`.
- **Initializer:** installed `pmcro-commands:init` Python handler copied unchanged; missing `config.py`, `profile_builder.py` and `audit.py` were supplied by new, clearly labeled recovery implementations. They are not asserted to be the lost original modules.
- **Actual profile generation:** `PASS` in [run 45116577877](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45116577877).
- **Independent verification:** `PASS`, 22 generated files checked by SHA-256, seven skills, 11 managed domains and five memory partitions.
- **Receipt ID:** `e13e04f07ef93b03812a52df0b3bcb179826db58c5eb4b8009dd3cf0956cf7f1`.
- **Idempotence:** second `write` returned `ALREADY_INITIALIZED`.
- **Negative tests:** five source/receipt/skill corruption and rollback tests passed.
- **.NET MCP:** independently [PASS in run 45115843847](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45115843847): MCP `initialize`, `tools/list`, `tools/call Add(2,3)` returned `5`.
- **Native PMCR-O execution, MAF, A2A:** `NOT_INVOKED`.

The generated `.pmcro/` profile was built inside the CI runner's temporary project directory, not committed to this repository and not installed on the user's Windows host. Its receipt, manifest and logs were uploaded as CI evidence.
