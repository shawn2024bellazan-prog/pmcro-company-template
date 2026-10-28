# PMCR-O glossary (definitions)

These are plain-English meanings for terms used throughout the repository. Some are established software terminology; others are PMCR-O application vocabulary. **An explanation is not evidence that a feature is running.** Use [Project status](PROJECT-STATUS.md) for that.

| Term | Plain-English meaning | Why it matters |
| --- | --- | --- |
| **Agent** | A software participant assigned a job or role | A role label alone does not prove identity |
| **I AM / `i_context`** | A structured statement of who an agent says it is, its role and its task lineage | The runtime must compare it with trusted records; a self-claim is untrusted |
| **Human orchestrator** | The person who sets goals and grants approvals within their real permissions | Not the same as an authenticated machine agent |
| **Orchestrator agent** | Coordinates the workflow, budgets and next steps | Does not invent authority |
| **Planner** | Proposes steps and acceptance criteria | Plans are not permission to execute |
| **Maker** | Performs a bounded authorized change | Must report observed results and commit evidence |
| **Checker** | Independently inspects results against requirements | Its PASS must come from a real trusted Checker, not a string written by the Maker |
| **Caller / parent agent** | The actor that delegated a task | Owns decisions about continuing after the child returns |
| **Subagent / child** | An agent asked to handle part of the parent's work | Its result must be correlated with the parent run |
| **Handoff** | A proposed transfer of work to another role | May require human and policy approval |
| **NextSeed** | A structured suggestion for the next unit of work | `proposed` never means authorized or executed |
| **Cognitive loop** | The ORCHESTRATE → PLAN → MAKE → CHECK → REFLECT cycle | Each cycle can finish, loop, defer, halt or escalate |
| **Trail Frame** | A structured audit/evidence event about an action or decision | Record who, what, when, why, policy and evidence |
| **Cognitive Trail** | The sequence of related Trail Frames | Useful for accountability, audit and reconstruction |
| **Correlation ID** | An identifier connecting messages in one task flow | Prevents unrelated results from being mistaken for this task |
| **Run ID** | A specific execution instance | Distinguishes attempts and delegated runs |
| **Causation ID** | An identifier for the command/event that caused another | Establishes why a message happened |
| **Canonical** | One authoritative, validated form of a contract | Prevents conflicting role or lineage declarations |
| **Echo / echoed identity** | When a child merely repeats a claimed parent address or token | An echo is **not** proof; the host compares it to a trusted source. This is an explanation, not a separate PMCR-O protocol primitive |
| **Policy** | Rules governing what is allowed or needs approval | Applied by trusted host services, not by the child |
| **Bound policy / policy-bound authorization** | An approval tied to a *specific* result digest, seed, actor, Checker evidence and policy version | A different result or changed policy should not inherit the original approval |
| **Policy version / epoch** | Identifier of the policy rules in effect | Reservation must detect an approval made under an outdated policy |
| **Authority ceiling** | Maximum permissions or scope an actor is allowed to use | Delegation cannot widen this ceiling by claiming a new role |
| **Fail closed** | If identity, proof, policy or storage is missing, the operation is blocked or deferred | Safer than assuming permission |
| **Digest / SHA-256** | A fingerprint of data | Detects changed content; a hash by itself is not authentication |
| **HMAC / signed proof** | A message authentication code made using a protected host key | Can detect unauthorized modification only if key custody is secure |
| **Provenance** | Where evidence came from and how it was produced | A matching frame ID is insufficient without trusted origin |
| **Tamper-evident** | Alterations can be detected, especially against a separately trusted anchor | Does not mean impossible to alter |
| **Trail-head anchor** | Trusted record of the latest expected hash of a Trail chain | Needs a trust boundary outside the mutable Trail database |
| **Idempotency** | Guard against executing the same logical operation more than once | A duplicate request should not cause a duplicate side effect |
| **Compare-and-swap (CAS)** | Update only if the stored version is still the expected version | Prevents two concurrent processes from both winning |
| **Transactional** | Multiple storage changes succeed or fail as one unit | Critical for policy evidence plus dispatch reservation |
| **Outbox** | Durable record of an intended external operation | Allows recovery, auditing and cautious dispatch |
| **Dispatch** | Actually sending/triggering the authorized operation | Different from writing an outbox reservation |
| **External operation ID** | Identifier provided or used for an outside action | Needed to reconcile whether it ran |
| **Execution uncertain** | An operation might have run, but the system cannot prove the outcome | Do **not** automatically replay; investigate |
| **Reconciliation** | Check authoritative external evidence to settle an uncertain operation | Enables a safe terminal record without guessing |
| **Deferred** | Decision postponed pending evidence, policy or infrastructure | Should be recoverable without claiming work was done |
| **Duration / deadline / timeout** | Elapsed time, end time allowed, or a deadline that was exceeded | A timeout is *not* proof that no external side effect occurred |
| **Activation** | Enabling a registered capability in a real authorized runtime | Different from a demo, passing tests or talking in an agent role |
| **Promotion** | Governance approval to move an artifact to the next environment or state | Not implied by CI PASS |
| **CI (continuous integration)** | Automated builds, checks and tests on a commit | Green CI is evidence of tested code, not production deployment |

## What do PASS, LOOP, HALT and BLOCKED mean?

- **PASS:** independently verified acceptance criteria were met. Only use with genuine evidence; distinguish CI success from Checker approval.
- **LOOP:** a bounded correction is still needed.
- **HALT:** stop due to safety, invalid authorization or another hard guard.
- **BLOCKED:** essential infrastructure, permission or evidence is missing; no safe progress can be claimed.

## Where to go next

- [Start here](START-HERE.md)
- [Current status](PROJECT-STATUS.md)
- [Subagent-return contract](architecture/subagent-return.md)
- [Command/message/Trail contract](architecture/command-message-trail-contract.md)
