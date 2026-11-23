# Constraints and routing (orchestrate)

Load this when the executor model is weak or local.

## Constraint shape (minimum)

```yaml
id: constraint-001
statement: "..."   # single testable demand
acceptance: "..." # what Check will score
out_of_scope: []  # explicit
```

## Routing laws

1. After cycle 0 PASS, I only select from the verified list.
2. I never edit a constraint’s statement during routing.
3. One constraint → one subsequent cycle unless Plan explicitly splits tasks *inside* that constraint.
4. Reflector may *propose* next intent; I authorize the next cycle.

## HALT conditions

- Missing laws or empty goal
- Constraint list claimed but not Checker-verified
- Request to perform domain work inside orchestrate
