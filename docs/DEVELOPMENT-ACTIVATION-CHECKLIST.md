# Development-runtime activation checklist

**Audience:** The human owner/orchestrator of PMCR-O and the trusted host operator.

**Current state:** This is a development-readiness checklist, **not** an activation request or a claim that production agents are running. The `pmcro.subagent-return` plugin is disabled by default. A normal chat command does not create authenticated agent identities, registry grants, or an independent Checker.

## Before an authorized development activation

- [ ] **Choose a non-production environment** (development only), with a named human owner and rollback contact.
- [ ] **Confirm the exact commit and CI run** for the code being activated. Source code and green tests are necessary but not sufficient.
- [ ] **Run the .NET 10 build and contract suite** in the intended environment and retain real logs, not invented Trail Frames.
- [ ] **Confirm a runtime host exists** and that the host, not a chat prompt or child agent, authenticates each agent and user.
- [ ] **Identify your human approval scope**: which development projects, roles, plugins and routes you are legally/technically authorized to enable.
- [ ] **Review the agent charter** in [AGENTS.md](../AGENTS.md): the Orchestrator coordinates, Maker implements, Checker independently verifies, and the caller owns continuation.
- [ ] **Configure the trusted plugin registry and grant service**, leaving unknown routes and all external side effects disabled by default.
- [ ] **Configure trusted Checker proof issuing and verification** with an independently authenticated Checker identity. Secure key custody, rotation, secret access boundaries and revocation must be demonstrated. Never place real keys in source or tests.
- [ ] **Configure the current policy authority and version updates** to the SQLite policy-version mirror. A test fixture or Boolean child claim does not authorize dispatch.
- [ ] **Validate authorization snapshots** bound to the exact result digest, NextSeed, caller/run/correlation IDs, policy scope/version, Checker frame and evidence.
- [ ] **Configure a separately governed persistent Trail-head anchor**, audit write access, and verify the exact source Checker frame appears in the anchored chain. The in-memory anchor is *test-only*.
- [ ] **Verify durable return state and outbox backups, locking and restart procedures.** A reserved dispatch must not be automatically replayed.
- [ ] **Provide an authenticated external operation ID and reconciliation service** *before* enabling any real side-effecting dispatch. The reference durable handler currently records/defer intents without calling an external dispatcher.
- [ ] **Obtain an actual separately authenticated Checker verdict** on the frozen implementation commit, including policy changes, concurrent requests, forgery and crash/restart.
- [ ] **Record explicit human approval for a named development environment and exact plugin version and scope**. Approval of development does not grant production activation.

## What you, the human orchestrator, decide

You define the objective and accepted changes; you may approve an **explicitly scoped development activation** after the above requirements pass. You also decide how to handle a failed Checker verdict, unexpected external side effect, missing evidence, security incident or exhausted recursion budget.

Your approval must be applied by a trusted runtime host. A message beginning `@pmcro-orchestrator /pmcro:seed` expresses intent but does **not** itself grant privileges.

## What each status means

| State | Plain-English meaning |
| --- | --- |
| **Development code verified** | CI passed for a specific commit |
| **Staged intent** | Authorized continuation data was durably recorded, but nothing external was dispatched |
| **Dispatch reserved** | One external operation ID and intent were reserved; crash recovery must reconcile it |
| **Execution uncertain** | Side effects may have occurred; never blindly retry |
| **Checker PASS** | Actual independent evidence confirms bounded acceptance criteria |
| **Activated (development)** | A trusted host registered and enabled the exact development-scoped capability with explicit approval |
| **Activated (production)** | Separate production permissions, evidence, credentials and deployment gates completed—not implied by development success |

## Live status of this repository

The SQLite reference coordinator, hash-linked ledger, source/Checker proof contracts and fail-closed acceptance gates are developmental components. The external production dispatcher, real protected Checker key service and independently governed Trail anchor are not shown to be deployed. No release, plugin activation, merge or promotion is performed by this checklist.

See [Start Here](START-HERE.md), [Glossary](GLOSSARY.md), and [Project Status](PROJECT-STATUS.md) for context.
