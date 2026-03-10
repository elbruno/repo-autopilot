# Agent Design Deep Dive: How Intent-Driven Agents Power Repository Maintenance

## How Intent-Driven Agents Work

When you say "clean stale branches," how does the system understand and execute your request?

## Intent Parsing: From English to Operations

The Intent Pipeline transforms natural language into executable operations:

```
"Clean stale branches"
    ↓
[Lexical Analysis] → tokenize, part-of-speech tagging
    ↓
[Semantic Parsing] → extract intent (CLEANUP), objects (BRANCHES), modifiers (STALE)
    ↓
[Copilot SDK Analysis] → resolve ambiguities
    - What's "stale"? (no commits in 30 days)
    - Any exceptions? (protected branches, recent activity?)
    - Scope? (all branches or a pattern?)
    ↓
[Structured Intent] → {"action": "cleanup_branches", "filter": "stale", "threshold": "30 days", "scope": "all"}
    ↓
[Agent Dispatch] → route to appropriate agents
```

### Example Intent Resolutions

| User Input | Parsed Intent | Parameters |
|-----------|--------------|-----------|
| "Clean stale branches" | cleanup_branches | threshold=30d, exclude=protected |
| "Delete unused branches from 2024" | cleanup_branches | creation_before=2024-12-31, exclude=merged |
| "Find merge conflicts" | analyze_conflicts | scope=all, strategy=semantic |
| "Suggest refactoring opportunities" | analyze_code_quality | scope=all, focus=duplication |

## The Five-Agent Orchestration Model

The system operates as a coordinated network of autonomous agents. Each agent has specific responsibilities:

### 1. Intent Agent

**Responsibility:** Parse user intent, clarify ambiguities, verify permissions.

**Actions:**
- Tokenize and parse natural language input
- Use Copilot SDK to resolve ambiguities
- Ask clarifying questions if needed
- Escalate if request is dangerous

**Example Dialog:**
```
User: "Clean up old branches"
Intent Agent: "Found intent: cleanup_branches. Need clarification:
  1. What's 'old'? (suggest: no commits in 30 days)
  2. Keep protected branches? (main, develop, release/*)
  3. Keep branches with unmerged commits? (default: no)
  
Proceed with defaults? (y/n)"
```

### 2. Analysis Agent

**Responsibility:** Crawl repository, gather metadata, detect patterns.

**Actions:**
- Enumerate all local branches
- For each branch: fetch metadata (last commit, author, merge status)
- Build branch dependency graph
- Identify stale branches, orphans, conflicts
- Flag anomalies (very old, very new, high churn)

**Output Example:**
```json
{
  "branches": [
    {
      "name": "feature/old-auth",
      "last_commit": "2025-09-10T14:22:00Z",
      "last_author": "alice@company.com",
      "commit_hash": "abc123",
      "merged_to_main": true,
      "unmerged_commits": 0,
      "days_stale": 157,
      "protected": false,
      "risk_score": 0.1
    },
    {
      "name": "bugfix/experimental",
      "last_commit": "2025-11-01T09:45:00Z",
      "last_author": "bob@company.com",
      "commit_hash": "def456",
      "merged_to_main": false,
      "unmerged_commits": 3,
      "days_stale": 135,
      "protected": false,
      "risk_score": 0.8
    }
  ],
  "conflicts": [
    {
      "branch_a": "feature/auth",
      "branch_b": "feature/api",
      "conflict_markers": 7,
      "conflict_lines": 42,
      "resolution_strategy": "merge_tool_required"
    }
  ]
}
```

### 3. Safety Agent

**Responsibility:** Evaluate risks, apply guardrails, plan rollback.

**Actions:**
- Dependency check: does anything depend on this branch?
- Merge check: is the branch already merged? Confirm commit identity.
- Orphan check: is there unmerged work that would be lost?
- Protection check: is the branch protected? Escalate.
- Recency check: recent activity despite age? Require confirmation.
- Compute risk score for each operation

**Risk Scoring Formula:**
```
Risk Score = (days_stale / threshold) × (unmerged_commits / 10) × protection_factor

- 0.0-0.3: ✓ Safe to delete automatically
- 0.3-0.7: ⚠️  Requires manual review
- 0.7-1.0: 🔒 Requires escalation
```

**Safety Checks Example:**
```
Branch: feature/old-auth
├─ Dependency check: ✓ PASS (no dependencies)
├─ Merge check: ✓ PASS (merged to main, commit matches)
├─ Orphan check: ✓ PASS (no unmerged commits)
├─ Protection check: ✓ PASS (not protected)
├─ Recency check: ✓ PASS (no recent pushes)
└─ Risk Score: 0.15 → ✓ SAFE

Branch: bugfix/experimental
├─ Dependency check: ✓ PASS
├─ Merge check: ✗ FAIL (not merged, 3 unmerged commits)
├─ Orphan check: ✗ FAIL (contains unique work)
├─ Protection check: ✓ PASS
├─ Recency check: ⚠️  WARNING (30 days no push, but unmerged work)
└─ Risk Score: 0.75 → ⚠️  REQUIRES REVIEW
```

### 4. Execution Agent

**Responsibility:** Apply changes, maintain transaction semantics, handle errors.

**Actions:**
- Create transaction checkpoint (git state snapshot)
- Execute deletions/changes in sequence
- Log each operation with justification
- Handle errors gracefully; rollback if needed
- Create rollback script

**Transaction Model:**
```
1. Snapshot current state
   └─ git rev-parse HEAD > .repoauto/checkpoint-xyz
   └─ git show-ref > .repoauto/branch-index-xyz

2. Execute operations (in safe order)
   ├─ Delete branch-a
   ├─ Delete branch-b
   ├─ ... 
   └─ Log: "Deleted 15 branches"

3. Commit transaction
   └─ Write audit log
   └─ Create rollback script

4. Error Handling
   └─ If operation fails, rollback to checkpoint
   └─ Escalate to user with context
```

**Rollback Script Example:**
```bash
#!/bin/bash
# Auto-generated rollback script
# Cleanup Session: 2026-03-15 14:32:05

echo "Rolling back cleanup..."

# Restore branches from backup
git branch feature/old-auth abc123
git branch feature/deprecated-dashboard def456
# ... restore all 15 branches

echo "Rollback complete. Your repository is restored."
```

### 5. Reporting Agent

**Responsibility:** Summarize results, flag exceptions, guide next steps.

**Sample Output:**
```
═══════════════════════════════════════════════════════════════
  REPOSITORY CLEANUP REPORT
  Repository: ~/dev/my-project
  Started: 2026-03-15 14:32:05
  Duration: 2.3 seconds
═══════════════════════════════════════════════════════════════

SUMMARY:
  • Analyzed 47 branches
  • Identified 18 stale branches
  • Deleted 15 branches (safe)
  • Flagged 2 for review (unmerged work)
  • Preserved 30 branches (active/protected)

DELETED BRANCHES:
  ✓ feature/old-auth (157 days)
  ✓ feature/deprecated-dashboard (145 days)
  ✓ hotfix/temp-fix (138 days)
  ... (12 more)

FLAGGED FOR REVIEW:
  ⚠️  bugfix/experimental (3 unmerged commits)
  ⚠️  refactor/pending (1 unmerged commit)

TECHNICAL DEBT DETECTED:
  • 2 merge conflicts in active branches
  • 3 unused dependencies
  • Legacy code patterns in 5 files

NEXT STEPS:
  1. Review flagged branches: git show bugfix/experimental
  2. Merge or delete flagged branches
  3. Run cleanup again: dotnet run -- cleanup ~/dev/my-project

ROLLBACK:
  Available: YES
  Command: ./repos/rollback-2026-03-15.sh

═══════════════════════════════════════════════════════════════
```

## Agent Communication & Conflict Resolution

Agents communicate through a message queue:

```
Intent Agent      →  "cleanup_stale_branches(threshold=30d, scope=all)"
Analysis Agent    →  "analysis_complete: 18 stale, 2 conflicts, risk_data=[...]"
Safety Agent      →  "safety_check_passed: 15 safe, 2 require_review"
Execution Agent   →  "cleanup_executed: 15 deleted, 0 errors"
Reporting Agent   →  "report_generated: see cleanup-2026-03-15.log"
```

### Conflict Resolution Example

```
Scenario: Safety Agent disagrees with Analysis Agent

Analysis Agent says: "Branch X is stale (157 days)"
Safety Agent says: "But it has recent activity (pushed 10 days ago)"

Resolution:
1. Agents escalate to Intent Agent
2. Intent Agent re-parses context
3. Ask user: "Branch X is old but recently touched. Review before delete?"
4. User approves or declines
5. Execute based on user decision
```

## Copilot SDK Integration

The Copilot SDK enhances three key operations:

### 1. Intent Disambiguation

User says "clean up old stuff"
→ SDK resolves "old" to "no commits in N days"
→ Proposes reasonable thresholds: 30d, 60d, 90d

### 2. Semantic Conflict Detection

- Analyzes merge conflicts semantically
- Suggests resolution strategy: auto-merge safe, manual for complex
- Rates confidence in each suggestion

### 3. Code Quality Analysis

- Detects dead code, unused imports, patterns
- Suggests refactoring opportunities
- Flags performance red flags

## Local-Only Architecture: Why It Matters

The entire orchestration runs locally:

```
┌─────────────────────────────────────┐
│   Your Machine                      │
├─────────────────────────────────────┤
│  Intent Agent                       │
│  Analysis Agent                     │
│  Safety Agent                       │
│  Execution Agent                    │
│  Reporting Agent                    │
│                                     │
│  ↓ (no external calls)              │
│                                     │
│  Git Operations (local)             │
│  File I/O (local)                   │
│  Copilot SDK (embedded)             │
│                                     │
│  Your Repositories                  │
└─────────────────────────────────────┘
       ↑
       │ Zero external dependencies
       │ (except initial SDK download)
```

### Security & Privacy Implications

- **No code uploaded to cloud** — All analysis happens locally
- **No intent exposed to external services** — Your commands stay on your machine
- **No branch names leak to analytics** — Repository structure is private
- **Fully auditable locally** — Every action is logged and reviewable
- **Complies with GDPR, HIPAA, SOC2** — No data leaves the organization

## The Agentic Loop: Real-World Walkthrough

Here's how agents work together on a real cleanup request:

**User Input:** "Clean up branches from 2025"

1. **Intent Agent** parses request
   - Interprets "branches from 2025" as "created before 2025-12-31"
   - Confirms: "Should I also consider branches merged to main?"
   - Receives confirmation; proceeds

2. **Analysis Agent** crawls repository
   - Finds 156 branches created in or before 2025
   - Identifies 34 stale, 12 already merged, 4 protected
   - Flags 3 with unmerged commits

3. **Safety Agent** evaluates each candidate
   - Confirms merge status with Git
   - Checks for dependent branches
   - Computes risk scores
   - Result: 23 safe, 11 require review

4. **Execution Agent** (with approval)
   - Creates checkpoint: branch state snapshot
   - Deletes 23 branches in safe order
   - Logs each deletion
   - Saves rollback script

5. **Reporting Agent** generates summary
   - "Cleaned 23 old branches (2025+)"
   - "Flagged 11 for manual review"
   - "Repository health improved: 67% → 78%"

---

**Next:** Explore the [API Reference](api-reference.md) to understand available commands and configuration options.
