# Work Routing

Routes work from Squad (Coordinator) to team members based on signal.

## Routing Table

| Domain | Primary | Backup | Trigger Words |
|--------|---------|--------|----------------|
| Architecture & SDK decisions | Keaton | — | "architecture", "design", "sdk", "strategy", "review" |
| C# implementation & agents | Fenster | Keaton | "code", "build", "implement", "agents", "orchestration" |
| Testing & safety guardrails | Hockney | Fenster | "test", "verify", "safety", "conflicts", "edge cases" |
| Documentation & narrative | McManus | Keaton | "docs", "write", "narrative", "content", "hooks" |
| Work queue & backlog | Ralph | Keaton | "ralph", "status", "board", "backlog", "monitor" |
| Session logs & memory | Scribe | — | (automatic after work completes) |

## Principles

1. **Single agent preferred** unless the request spans 3+ distinct domains (in that case, fan-out parallel)
2. **Lead (Keaton)** breaks ties and owns cross-domain decisions
3. **Scribe fires background** after any substantial agent work — never blocks
4. **Eager execution** — spawn all agents who could usefully start work in parallel as `mode: "background"`
5. **Quick facts answer directly** — don't spawn an agent for factual questions from context
6. **Anticipatory work** — if implementation starts, spawn tester simultaneously to write test cases from requirements

