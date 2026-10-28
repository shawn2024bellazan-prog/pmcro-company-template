# PMCR-O marketplace action-download — candidate work record

**Intent:** Add G: candidate integration artifacts to GitHub in a separate, non-runtime tree, and author a locally testable marketplace skill for *staging* owner-approved action downloads by exact SHA-256. No release, registration, grant, activation, secret, model or worker use is authorized.

**Design root:** `G:\workspace\pmcro-company-platform`. **G plugin root:** `G:\plugins`. **Remote development branch:** `feat/pmcro-subagent-return-v1` in `shawn2024bellazan-prog/pmcro-company-template`.

**State:** PROPOSED / UNAUTHENTICATED authoring record, not a persisted Trail Frame. Existing Seed 0033 HALT and Seed 0034 UNEXECUTED are preserved.

**Safety:** Only create a candidate marketplace product and MAF-native skill with `SKILL.md` plus a Python stdlib staging script; no manifest status changes. An action download requires a separately supplied exact HTTPS source, expected SHA-256, owner-directed staging, and manual invocation. The script cannot install or execute downloaded content; action source is untrusted until verified by host and Checker.

**Verification:** Offline argument/security tests; manually read files, run Python tests; push candidate snapshot under `candidate/g-company-platform/` only; verify GitHub CI as observed. No independent Checker receipt is expected from these authoring steps.

**Open questions:** Owner has not chosen an action artifact/immutable URL and digest; so no actual external action download should be attempted. `act` local runner not found on current PATH, and this candidate skill is not the same as installing `act`.