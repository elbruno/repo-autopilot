# Keaton — History

## Core Context

**Project:** Automating Local Repository Maintenance with GitHub Copilot SDK and Intent-Driven Agentic Workflows  
**User:** Bruno Capuano  
**Repository:** C:\src\localRepoAuto  
**Universe:** Reservoir Dogs (Lead agent)

## Learnings

### 2026-03-10: Phase 1 Architecture Foundation

Completed Phase 1 architectural deliverables:

1. **Intent Schema & Action Mapping** — Defined 11 intents across 4 categories (Branch Hygiene, Conflict Resolution, Repository Health, Audit & Compliance). Each intent follows Intent → Preconditions → Actions → Postconditions structure.

2. **Agent Framework Architecture** — Designed around Microsoft Agent Framework with:
   - `IAgent` interface with `AgentBase` abstract class
   - Synchronous request-response communication (not pub/sub)
   - Session-scoped state management
   - Orchestration via `IntentRouter`, `AgentManager`, `SessionCoordinator`

3. **Copilot SDK Integration** — Strategy for optional AI enhancement:
   - `ICopilotService` abstraction over SDK
   - Graceful degradation to deterministic algorithms when unavailable
   - Local-only execution; SDK is the only network dependency
   - Conservative confidence thresholds (0.85) for auto-resolution

4. **Safety Architecture** — Five-level risk classification (L0-L4) with:
   - Safety gate pattern (`ISafetyGate`, `ISafetyCheck`)
   - Protected branch patterns, unmerged checks, working tree validation
   - File-based audit trail with rollback support
   - Reflog-based recovery architecture

**Key Decisions Made:**
- Synchronous over async messaging (simplicity for local tool)
- Session-scoped state over persistence (Git is source of truth)
- Conservative defaults (dry-run, high confidence thresholds)
- Fail-safe preconditions (abort on uncertainty)
