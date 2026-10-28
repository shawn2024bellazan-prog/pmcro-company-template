# PMCR-O subagent return (`pmcro.subagent-return.v1`)

Status: candidate plugin on a feature branch. It is **disabled by default**, not
registered, not merged, not deployed and not promoted (`promotion: generated`).

## Purpose

A caller delegates work to a subagent and later needs one of two things, never a
silent gap: a **correlated terminal result**, or a **caller-owned failure frame**
that records why there is none. This plugin defines that terminal return and the
caller-side completion handler. It reuses, and does not replace, the existing
continuation contracts (`CallerContinuationHandler`), the native command router
(`PmcroApplicationCommandRouter`) and the Enterprise Trail store.

## Relationship to VS Code concepts (design inspiration only)

These are borrowed as vocabulary. They are VS Code product behavior, not PMCR-O
authority, and nothing here depends on VS Code being present.

| VS Code concept | PMCR-O treatment |
| --- | --- |
| A custom agent can run as a subagent so its work stays out of the main conversation's context ([custom agents](https://code.visualstudio.com/docs/agent-customization/custom-agents)) | Context isolation maps to the **I AM context**: the child states who it is and what run it belongs to; the parent never merges the child's context into its own. |
| Handoffs are explicit, user-selected transitions between agents ([custom agents](https://code.visualstudio.com/docs/agent-customization/custom-agents)) | A `NextSeedEnvelope` inside a return is a **handoff proposal**. It is routed through the caller's continuation handler and is never executed on the child's say-so. |
| `SubagentStart` / `SubagentStop` hook events; a stop hook can block a subagent from stopping ([hooks reference](https://code.visualstudio.com/docs/agents/reference/hooks-reference)) | Lifecycle states (`completed`, `interrupted`, `timed_out`, `failed`, `cancelled`) are **observed or claimed, then validated**. A lifecycle claim is evidence for the caller to weigh, not a grant. |

The VS Code documentation does not describe how a subagent's result is returned
to its parent, so the return envelope below is a PMCR-O design, not a mirror of VS Code.

## Message: `SubagentReturnEnvelope`

JSON field names are snake_case. `i_context` is the child's self-declared identity:

| Field | Meaning |
| --- | --- |
| `schema`, `message_type` | `pmcro.subagent-return.v1`, `subagent.return` |
| `i_context` | `agent_id`, `role`, `intent_id`, `run_id`, `correlation_id`, `causation_id`, `parent_frame_id`, `subagent_run_id` |
| `lifecycle_state` | one of `completed`, `interrupted`, `timed_out`, `failed`, `cancelled` |
| `stop_reason` | child free text; **never copied** into the Trail |
| `reply_to`, `return_route` | parent return routing; compared with the caller's trusted record |
| `final_result` | an existing `CycleResultEnvelope`; required for `completed`, forbidden otherwise |

The I AM context is an **untrusted assertion**. The host supplies a
`TrustedSubagentInvocation` (caller identity, expected child, lineage, expected return
route, subagent run id, plugin route and an optional host-observed lifecycle state) from
its own ledger. Every I AM field is compared ordinally with that record. A blank trusted
value never matches a blank child value; an incomplete binding is rejected.

## Components

- `SubagentReturnEnvelope`, `IAmContext`, `SubagentReturnParser`: the contract and a
  parser that maps untrusted JSON to stable codes (`malformed_envelope`,
  `missing_i_context`, `malformed_i_context`).
- `SubagentReturnValidator`: pure, fail-closed validation. It separates *identity
  verified* (I AM matches the trusted record) from *structurally valid*.
- `CallerFailureFrameFactory`: builds Trail Frames **owned by the caller** with
  `failure-owner:caller`, a `failure-attribution:{child|host|caller}` and reason code.
  Frames never assert a checker `PASS` (`CheckerVerdict: NOT_CHECKED`).
- `TrailFrameIntegration`: validates and appends frames to the existing store, checks the
  caller's parent frame really exists, and reads correlated frames for checker proofs.
- `SubagentCompletionHandler`: the decision flow below.
- `SubagentReturnCommandHandler` and `SubagentReturnComposition`: adapt the handler to the
  native command router so outer authorization and idempotency apply first.

## Decision flow (every step fails closed)

1. **Trusted context complete?** Otherwise rejected with no Trail write (no run to attribute to).
2. **Plugin gate:** manifest valid, route declared, grant registered, **enabled**, and
   matching artifact, version and policy reference. A disabled plugin defers with a trailed
   decision and performs no checker, policy or dispatch call.
3. **Parent frame exists** in the caller's Trail, else rejected and attributed to the caller.
4. **Host-observed lifecycle** (for example a deadline the host enforced) outranks the child.
   A later "success" from the child is a duplicate, never accepted.
5. **Identity validation.** An unauthenticated return is rejected **without consuming** the
   single terminal-return claim, so a forged message cannot block the genuine child.
6. **Atomic claim** per (caller, intent, run, subagent run). A second authenticated return is `Duplicate`.
7. **Lifecycle:** `interrupted`, `timed_out`, `failed`, `cancelled` and a `completed` return with no
   final result each produce a caller-owned failure frame and stop.
8. **Completed:** the child's checker verdict must be `passed`; then an **independent checker
   proof** (`ISubagentCheckerProof`) must verify from governed evidence and name a checker that
   is neither the maker nor the child. Only then is a proposed next seed handed to
   `CallerContinuationHandler`, which re-validates lineage, verifies Trail evidence, evaluates the
   **caller's current policy**, claims idempotency and writes a Trail decision before dispatching.

`UnconfiguredSubagentCheckerProof` is the default and verifies nothing, so an unconfigured
host accepts no completed return.

## Failure attribution

| Attribution | Examples |
| --- | --- |
| `child` | malformed return, missing final result, child-reported interruption or failure |
| `host` | host-observed timeout, checker outage, claim-store outage, invalid grant |
| `caller` | the caller's own parent frame is missing from its Trail |

The caller authors every failure frame, because a child that never returned or returned
garbage cannot be relied on to record its own failure.

## Not implemented here

Production resolvers, a durable shared claim store (the supplied in-memory store is a
single-process reference), a real independent checker adapter, live transport adapters and
plugin registration. Registration and enablement are host and policy decisions. Merging,
deploying, activating or promoting this plugin requires a separate approved step.

## Checks

`tests/PMCR.O.SubagentReturn.Tests` runs 18 checks with no third-party packages. Run
`dotnet run --project tests/PMCR.O.SubagentReturn.Tests/PMCR.O.SubagentReturn.Tests.csproj -c Release`
or `scripts/validate.ps1`.
