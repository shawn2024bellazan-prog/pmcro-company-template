# pmcro-orchestrator

High-level goal specialist plugin — analog of [`plugins/dotnet11`](https://github.com/dotnet/skills/tree/main/plugins/dotnet11).

## Skills

| Skill | Role |
|-------|------|
| [orchestrate](skills/orchestrate/) | Admit high_level_goal, trail, authorize cycles, route |
| [decompose-goal](skills/decompose-goal/) | Cycle-0 skill: high_level_goal → discrete constraints |

Orchestrate **calls** decompose-goal as a skill. Decomposition is not a private side power.

## Layout

```text
plugins/pmcro-orchestrator/
  plugin.json
  version.json
  README.md
  .claude-plugin/plugin.json
  .codex-plugin/plugin.json
  skills/
    orchestrate/
      SKILL.md
      references/
    decompose-goal/
      SKILL.md
      references/
```

Core cycle execution stays in **pmcro**.
