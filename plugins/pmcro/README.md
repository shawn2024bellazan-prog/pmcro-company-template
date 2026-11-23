# pmcro

Core PMCR-O lifecycle plugin — analog of [`plugins/dotnet`](https://github.com/dotnet/skills/tree/main/plugins/dotnet).

## Skills

| Skill | Phase | Role |
|-------|-------|------|
| [plan](skills/plan/) | plan | PlanFrame only; no execution |
| [make](skills/make/) | make | Execute plan; no verdict |
| [check](skills/check/) | check | PASS / LOOP / HALT only |
| [reflect](skills/reflect/) | reflect | Learning + propose next intent |

High-level goals are owned by **pmcro-orchestrator**, not this plugin.

## Layout

```text
plugins/pmcro/
  plugin.json
  version.json
  README.md
  .claude-plugin/plugin.json
  .codex-plugin/plugin.json
  skills/
    plan/SKILL.md
    make/SKILL.md
    check/SKILL.md
    reflect/SKILL.md
```
