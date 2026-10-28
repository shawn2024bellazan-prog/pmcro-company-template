# Start here — PMCR-O in plain English

PMCR-O is a governed, plugin-first framework for coordinating work across agents. **The repository is a development candidate, not an activated production service.** Its .NET 10 code and tests can run in a development environment. No chat message or self-declared `I AM` text authenticates or activates an agent.

## Read these first

1. [Glossary](GLOSSARY.md) — definitions of technical vocabulary, including policy binding, echoing, duration, Trail Frames, and idempotency.
2. [Project status](PROJECT-STATUS.md) — what exists, what is tested, and what is still missing.
3. [Agent operating contract](../AGENTS.md) — responsibilities and guardrails.
4. [Subagent return](architecture/subagent-return.md) — how a delegated agent returns to its caller.
5. [Continuation contract](architecture/next-seed-return-contract.md) — how a proposed next step is accepted or deferred.
6. [Approval and autonomy](architecture/approval-and-autonomy.md) — when work needs human authorization.
7. [Command/message/Trail contract](architecture/command-message-trail-contract.md) — the developer-facing protocol.

## Who is who?

| Participant | Responsibility | Can it authorize itself? |
| --- | --- | --- |
| **You, human owner/orchestrator** | Set goals, approve permitted changes and choose environments | You can approve within actual host/account permissions; a chat message is not a host credential |
| **Orchestrator agent** | Plan work, enforce budget, route work and results | No |
| **Planner** | Suggest tasks, acceptance checks and bounded plans | No |
| **Maker** | Execute only authorized tasks, report real results | No |
| **Checker** | Independently inspect implementation and evidence | No; must be an independently authenticated principal |
| **Reflector** | Summarize outcomes and recommend accept, retry, or escalate | No |
| **Caller** | The agent that delegated work; owns whether its child continues | No automatic promotion of child proposals |

**Important:** the roles above are responsibilities, not necessarily unique accounts or already-running programs. A conversation can *describe* this workflow; the runtime must verify actor identity, permissions, lineage, and Checker proof.

## The loop

1. **ORCHESTRATE:** clarify objective, limits, human authority and expected results.
2. **PLAN:** propose a small, testable step.
3. **MAKE:** implement only the approved work.
4. **CHECK:** verify independently and report PASS, LOOP, HALT or BLOCKED with evidence.
5. **REFLECT:** learn from the outcome and propose a *NextSeed*, without executing it automatically.

A real `NextSeed` is a **proposal**. It does not transfer permissions to the next actor.

## Two different meanings of "activate"

**Use the development demo:** from a checkout with .NET 10, run:

```bash
dotnet build PMCR-O.sln
dotnet run --project tests/PMCR.O.SubagentReturn.Tests
dotnet run --project src/PMCR.O.Demo
```

These commands build, test and run a demonstration. They **do not** register live external agents, enable the subagent-return plugin or activate production.

**Activate the governed runtime:** requires an operational host, trusted registry and grant, authorized policy/tenant context, durable state, trusted Checker evidence, configured secret custody, independent Trail anchoring, and explicit human authorization for the target environment. The plugin manifests remain disabled by default. Production activation is not established by this repository alone.

## Example request

```text
@pmcro-orchestrator /pmcro:seed "Plan a bounded change; require separate Checker evidence; do not deploy or activate."
```

This text states desired routing and intent. Whether it is a *real* command depends on a host that has authenticated the calling actor and resolved the route. Copy-pasting it into a normal chat does not create that host.

## Want one page that answers "Where is X?"

Use [Glossary](GLOSSARY.md) for **what a term means**, [Project status](PROJECT-STATUS.md) for **whether it works yet**, and the linked architecture pages above for **exact contracts and code**.
