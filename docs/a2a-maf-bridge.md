# PMCR-O A2A-to-native-MAF bridge proof

This proof requires real execution through **both SDKs in the same task**.

- A2A client discovers the native Agent Card and calls a named worker by JSON-RPC.
- The A2A SDK handler invokes a real Microsoft Agent Framework AIAgent.RunAsync.
- MAF returns the response; the A2A SDK publishes it as a completed task artifact.
- The verifier correlates the same A2A task ID with both the A2A handler record and the inner MAF core invocation.
- The worker definition and source orchestrator skill are both pinned by SHA-256.

The MAF adapter is deliberately thin: A2A owns the wire protocol and task lifecycle, MAF owns the internal agent invocation. No proprietary transport or fake invocations.

Passing this test does **not** mean that the five PMCR-O cognitive phases have executed or that a generative model has run; both remain separate gates.
