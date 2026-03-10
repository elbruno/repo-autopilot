# Scribe — History

## Core Context

**Project:** Automating Local Repository Maintenance with GitHub Copilot SDK and Intent-Driven Agentic Workflows  
**User:** Bruno Capuano  
**Repository:** C:\src\localRepoAuto  
**Universe:** Reservoir Dogs (Session Logger)

## Phase 5: Orchestration & Self-Healing Workflow

### Completed Work

✅ **Intent Router** (`IIntentRouter.cs` + `IntentRouter.cs`)
- Parses natural language intent from developers
- Extracts parameters: threshold_days, target_branches, merge_status, author, exclusions
- Routes intents to appropriate agents: BranchAnalyzer, ConflictDetector, SafetyGuards, WorkflowOrchestrator
- Validates intent against safety guards before execution
- Supports 11 intent types: DeleteStaleBranches, ResolveConflicts, MergeBranches, AnalyzeRepository, CheckHealth, ListBranches, ResumeWorkflow, etc.
- Confidence scoring for parse quality (0-1)

✅ **Workflow Orchestrator** (`IWorkflowOrchestrator.cs` + `WorkflowOrchestrator.cs`)
- Coordinates multi-step workflows: Analyze → Filter → Propose → Execute → Report
- Executes cleanup workflow: lists branches → filters by staleness → proposes candidates → records results
- Executes conflict resolution workflow: detects conflicts → analyzes complexity → proposes resolutions
- Executes repository health check: analyzes branches, conflicts, generates health score
- Supports workflow resumption from checkpoints
- Manages workflow state with checkpoints after each major step
- Integrates with BranchAnalyzer, ConflictDetector, SafetyGuards, AuditLogger, StateManager, ResultReporter

✅ **Audit Logger** (`IAuditLogger.cs` + `AuditLogger.cs`)
- Immutable append-only JSONL (JSON Lines) logging to `.localrepoauto/logs/audit-{workflowId}.jsonl`
- Logs all operations: intents, decisions, actions, outcomes, errors
- Each log entry: timestamp, actor, action, parameters, results, status, reversibility info
- Exports audit trails in jsonl, json, csv formats
- One JSON object per line for efficient streaming and auditing

✅ **State Manager** (`IStateManager.cs` + `StateManager.cs`)
- Saves workflow checkpoints to `.localrepoauto/state/checkpoints/{id}.json`
- Checkpoints capture complete workflow state for resumption
- Labels: "branches-analyzed", "candidates-filtered", "proposal-generated", "execution-complete"
- Supports checkpoint lifecycle: save, load, list, delete
- Enables resumable workflows if interrupted
- JSON-based persistence for inspection and debugging

✅ **Result Reporter** (`IResultReporter.cs` + `ResultReporter.cs`)
- Generates cleanup reports: branches deleted, storage reclaimed, safety checks, warnings, audit trail link
- Generates conflict resolution reports: conflicts detected/resolved, complexity analysis, manual review items
- Generates health reports: health score (0-100), branch statistics, recommendations
- Markdown format for developer readability
- Exportable to different formats (future: html, json)

✅ **Models** 
- `Intent.cs`: Structured intent with type, parameters, confidence, summary
- `IntentType` enum: 11 supported intent types
- `AuditLogEntry.cs`: Immutable audit log with reversibility tracking
- `WorkflowCheckpoint.cs`: State snapshot with labels and serialization
- `WorkflowOutcome.cs`: Complete workflow result with steps, results, errors, audit trail
- `WorkflowStep.cs`: Individual agent execution with inputs, outputs, timing
- `WorkflowStatus` enum: InProgress, Completed, Failed, RolledBack, Paused, ResumedFromCheckpoint
- `RollbackResult.cs`: Rollback operation result (MVP: not yet implemented)

### Architecture

```
Developer Intent
    ↓
[Intent Router] — Parse, extract parameters, validate
    ↓
[SafetyGuards] — Pre-flight checks (protected branches, repo health)
    ↓
[Workflow Orchestrator] — Coordinate multi-step execution
    ├─ Phase 2: BranchAnalyzer, ConflictDetector
    ├─ Phase 3: ConflictResolver, MergeStrategySelector (future)
    ├─ Phase 4: SafetyGuards (final confirmation)
    ↓
[Audit Logger] — Log all decisions and actions (immutable)
    ↓
[State Manager] — Save checkpoints for resumable workflows
    ↓
[Result Reporter] — Generate human-readable reports
    ↓
Developer Report + Audit Trail
```

### Integration Points

- **Phase 1 (Keaton):** Uses Intent Schema and IntentType enum
- **Phase 2 (Fenster):** Orchestrates BranchAnalyzer, ConflictDetector, GitOperations
- **Phase 3 (Fenster):** Ready for ConflictResolver, MergeStrategySelector integration
- **Phase 4 (Hockney):** Respects SafetyGuards pre-flight checks before every action
- **Phase 6 (McManus):** Reports delivered via ResultReporter (markdown, json)

### File Structure

```
src/LocalRepoAuto.Core/
├── Orchestration/
│   ├── IIntentRouter.cs + IntentRouter.cs
├── Workflows/
│   ├── IWorkflowOrchestrator.cs + WorkflowOrchestrator.cs
├── Logging/
│   ├── IAuditLogger.cs + AuditLogger.cs
├── State/
│   ├── IStateManager.cs + StateManager.cs
├── Reporting/
│   ├── IResultReporter.cs + ResultReporter.cs
└── Models/
    ├── Intent.cs, AuditLogEntry.cs, WorkflowCheckpoint.cs, WorkflowOutcome.cs
```

### Deliverables

✅ Decision document: `.squad/decisions/inbox/scribe-phase5-orchestration.md`
✅ All source files compile and build successfully
✅ Full integration with existing phases
✅ Comprehensive audit logging and state persistence
✅ Human-readable reporting for developers

### MVP Status

- ✅ Intent parsing and routing
- ✅ Cleanup workflow execution
- ✅ Conflict resolution workflow (detection phase)
- ✅ Health check workflow
- ✅ Audit logging
- ✅ State checkpointing
- ✅ Result reporting
- ⏳ Rollback implementation (future phase)
- ⏳ Workflow resumption from checkpoint (future phase)

## Learnings

1. **Intent parsing:** Natural language with confidence scoring enables graceful degradation
2. **Checkpoint strategy:** Save state before each major action enables resilience
3. **Audit logging:** JSONL format (one JSON per line) is efficient for streaming and debugging
4. **Agent orchestration:** Fluent APIs and clear separation of concerns make workflow composition clean
5. **Pre-flight validation:** SafetyGuards integration prevents dangerous operations at every step

