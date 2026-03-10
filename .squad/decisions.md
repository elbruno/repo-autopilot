# Squad Decisions

**Project:** Automating Local Repository Maintenance with GitHub Copilot SDK and Intent-Driven Agentic Workflows  
**User:** Bruno Capuano  
**Repository:** C:\src\localRepoAuto  
**Last Updated:** 2026-03-10  

---

## Initialization Decisions

### 2026-03-10: Team Casting & Universe

**Decision:** Cast the squad in the Reservoir Dogs universe.

**Rationale:** The project requires high-pressure orchestration and careful execution. Reservoir Dogs characters imply consequence, precision, and the friction of specialized roles working under constraints—a perfect fit for autonomous agents managing risky local operations.

**Team:**
- Keaton (Lead) — pressure and strategic thinking
- Fenster (Backend Dev) — meticulous implementation
- Hockney (Tester) — critical oversight and edge cases
- McManus (Technical Writer) — clear narrative under pressure
- Scribe (Logger) — persistent record-keeping
- Ralph (Monitor) — steady work management

---

## Architectural Decisions

### 2026-03-10: Phase 1 Foundation (Keaton)

**Decision:** Establish intent-driven architecture with Microsoft Agent Framework integration.

**Key Choices:**

| Area | Decision | Rationale |
|------|----------|-----------|
| Communication | Synchronous request-response | Simplicity for local execution, easier debugging |
| State | Session-scoped, not persistent | Git is source of truth; fresh sessions reduce stale state bugs |
| Safety | Five-level risk classification (L0-L4) | Granular control over automation boundaries |
| SDK Integration | Optional with fallback | Core features must work without Copilot SDK |
| Default Stance | Deny on uncertainty | Data protection priority for autonomous operations |

**Documents Created:**
- `.squad/decisions/inbox/keaton-intent-schema.md` — Intent vocabulary and action mapping
- `.squad/decisions/inbox/keaton-agent-framework.md` — C# agent interfaces and patterns
- `.squad/decisions/inbox/keaton-sdk-integration.md` — Copilot SDK integration strategy
- `.squad/decisions/inbox/keaton-safety-architecture.md` — Safety gates and guardrails

**Status:** Ready for team implementation. Fenster to code from these specs; Hockney to test safety gates.

---

## Scope & Feature Decisions

(To be filled as the project evolves)
