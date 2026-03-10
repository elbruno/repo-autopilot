# API Reference: Commands, Configuration, and Integration

Complete reference for commands, configuration schema, and Git operations.

## Command-Line Interface

### analyze

Analyze repository health without making changes.

```bash
dotnet run -- analyze <path> [options]
```

**Options:**
- `--format {json|table|summary}` — Output format (default: summary)
- `--detailed` — Include branch metadata and conflict analysis
- `--export <path>` — Save analysis to file
- `--conflict-detection` — Enable semantic conflict detection (slower)

**Example:**
```bash
dotnet run -- analyze ~/dev/my-project --detailed
```

**Output Example:**
```json
{
  "repository": "~/dev/my-project",
  "timestamp": "2026-03-15T14:32:05Z",
  "summary": {
    "total_branches": 47,
    "active_branches": 22,
    "stale_branches": 18,
    "protected_branches": 4,
    "merge_conflicts": 2
  },
  "branches": [...],
  "conflicts": [...]
}
```

### cleanup

Clean up stale branches and resolve issues.

```bash
dotnet run -- cleanup <path> [options]
```

**Options:**
- `--dry-run` — Preview changes without executing
- `--aggressive` — Higher threshold for deletion (45+ days stale)
- `--conservative` — Lower threshold (60+ days stale)
- `--exclude <pattern>` — Exclude branches matching pattern (e.g., `wip-*`)
- `--confirm` — Skip confirmation prompt
- `--rollback-script <path>` — Save rollback script to custom path

**Example:**
```bash
# Preview changes
dotnet run -- cleanup ~/dev/my-project --dry-run

# Execute with aggressive settings
dotnet run -- cleanup ~/dev/my-project --aggressive --confirm

# Exclude specific patterns
dotnet run -- cleanup ~/dev/my-project --exclude "wip-*" --exclude "temp-*"
```

### cleanup-all

Clean up all repositories configured in config.json.

```bash
dotnet run -- cleanup-all [options]
```

**Options:**
- `--parallel` — Process repositories in parallel
- `--report <path>` — Generate combined report
- `--dry-run` — Preview all changes

**Example:**
```bash
dotnet run -- cleanup-all --parallel --report cleanup-report.json
```

### config

Manage configuration.

```bash
dotnet run -- config [action] [options]
```

**Actions:**
- `init` — Create default config file
- `validate` — Validate current config
- `add-repo <path>` — Add repository to config
- `list` — List configured repositories

**Example:**
```bash
dotnet run -- config init
dotnet run -- config add-repo ~/dev/my-project
dotnet run -- config validate
```

### rollback

Execute a rollback script.

```bash
dotnet run -- rollback <script-path>
```

**Example:**
```bash
# Restore deleted branches
dotnet run -- rollback .repoauto/rollback/cleanup-2026-03-15.sh
```

## Configuration Schema

**File location:** `.repoauto/config.json`

### Complete Configuration File

```json
{
  "version": "1.0",
  
  "repositories": [
    {
      "path": "~/dev/my-project",
      "name": "my-project",
      "enabled": true,
      "protected_branches": ["main", "develop", "release/*"],
      "stale_threshold_days": 30,
      "exclude_patterns": ["wip-*", "temp-*"]
    }
  ],
  
  "safety": {
    "require_approval": true,
    "dry_run_first": true,
    "create_rollback_scripts": true,
    "audit_log": true,
    "max_operations_per_run": 50,
    "require_merge_confirmation": true
  },
  
  "agents": {
    "enable_semantic_analysis": true,
    "conflict_detection": true,
    "auto_report": true,
    "conflict_resolution_strategy": "semantic"
  },
  
  "scheduling": {
    "enabled": false,
    "frequency": "daily",
    "time": "02:00:00",
    "days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  },
  
  "logging": {
    "level": "info",
    "directory": ".repoauto/logs",
    "retention_days": 30,
    "include_detailed_metadata": true
  }
}
```

### Key Configuration Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `stale_threshold_days` | int | 30 | Days without commits before marking stale |
| `protected_branches` | array | ["main", "develop"] | Branches never deleted |
| `exclude_patterns` | array | [] | Branch patterns to never delete (e.g., `wip-*`) |
| `require_approval` | bool | true | Require user confirmation before cleanup |
| `dry_run_first` | bool | true | Always show preview before executing |
| `create_rollback_scripts` | bool | true | Generate undo script for each cleanup |
| `enable_semantic_analysis` | bool | true | Use Copilot SDK for conflict detection |
| `conflict_resolution_strategy` | string | "manual" | "semantic" for auto-resolution suggestions |
| `max_operations_per_run` | int | 50 | Max branches to process per cleanup session |
| `require_merge_confirmation` | bool | true | Require user to confirm branch merges |

## Git Operations API

These operations are called by agents internally; documented for reference and custom integrations.

### Branch Operations

```csharp
// List branches with metadata
IEnumerable<BranchInfo> ListBranches(string repoPath)

// Get detailed branch information
BranchInfo GetBranchInfo(string repoPath, string branchName)

// Detect stale branches
IEnumerable<BranchInfo> DetectStaleBranches(
  string repoPath, 
  int thresholdDays = 30
)

// Check if branch is merged
bool IsMerged(string repoPath, string branchName)

// Safely delete branch
void DeleteBranch(
  string repoPath, 
  string branchName, 
  bool force = false,
  bool log = true
)

// Get branch metadata
BranchMetadata GetMetadata(string repoPath, string branchName)
```

### Conflict Operations

```csharp
// Detect merge conflicts
IEnumerable<MergeConflict> DetectConflicts(string repoPath)

// Analyze conflict semantically
ConflictAnalysis AnalyzeConflict(
  string repoPath, 
  string filePath, 
  string conflictContent
)

// Suggest resolution
ResolutionStrategy SuggestResolution(ConflictAnalysis analysis)

// Apply conflict resolution
bool ResolveConflict(
  string repoPath,
  string filePath,
  string resolution
)
```

### Audit Operations

```csharp
// Log operation
void LogOperation(
  string repoPath, 
  OperationType type, 
  string details, 
  OperationStatus status
)

// Get audit history
IEnumerable<AuditLog> GetAuditHistory(
  string repoPath, 
  int limit = 100
)

// Create rollback script
string CreateRollbackScript(
  IEnumerable<Operation> operations, 
  string outputPath
)

// Export audit log
void ExportAuditLog(string repoPath, string outputPath)
```

## Copilot SDK Integration

These methods enable semantic analysis powered by the GitHub Copilot SDK.

```csharp
// Parse intent from natural language
Intent ParseIntent(string userInput)

// Resolve ambiguities in intent
Intent ClarifyIntent(Intent ambiguousIntent, string context)

// Analyze code for quality issues
CodeQualityAnalysis AnalyzeQuality(string filePath)

// Suggest refactoring
IEnumerable<RefactoringProposal> SuggestRefactoring(string filePath)

// Analyze merge conflict semantically
SemanticConflictAnalysis AnalyzeConflictSemantically(
  string fileContent,
  string branchAContent,
  string branchBContent
)

// Suggest safe resolution based on semantics
IEnumerable<ResolutionSuggestion> SuggestResolutions(
  SemanticConflictAnalysis analysis
)
```

## Output Formats

### JSON Output

```bash
dotnet run -- analyze ~/dev/my-project --format json
```

Returns structured data:
```json
{
  "repository": "~/dev/my-project",
  "timestamp": "2026-03-15T14:32:05Z",
  "summary": {
    "total_branches": 47,
    "active": 22,
    "stale": 18,
    "protected": 4
  },
  "branches": [
    {
      "name": "feature/auth",
      "last_commit": "2026-03-10T15:22:00Z",
      "days_stale": 5,
      "merged": false,
      "protected": false,
      "risk_score": 0.2
    }
  ]
}
```

### Table Output

```bash
dotnet run -- analyze ~/dev/my-project --format table
```

Renders as formatted table in terminal.

### Summary Output (Default)

```bash
dotnet run -- analyze ~/dev/my-project
```

Human-readable summary with key metrics.

## Exit Codes

- `0` — Success
- `1` — General error
- `2` — Configuration error
- `3` — Repository not found
- `4` — Permission denied
- `5` — User cancelled operation
- `6` — Safety check failed

## Logging

All operations are logged to `.repoauto/logs/` by default.

**Log levels:**
- `debug` — Verbose, for troubleshooting
- `info` — Standard operations (default)
- `warn` — Warnings (e.g., unusual conditions)
- `error` — Errors requiring attention

**Accessing logs:**
```bash
# View latest log
cat .repoauto/logs/latest.log

# View logs for specific date
cat .repoauto/logs/2026-03-15.log

# Search logs
grep "error" .repoauto/logs/*.log
```

## Troubleshooting

### Common Issues

**"Repository not found"**
- Verify path exists and is a Git repository
- Use absolute paths; `~` may not expand in config

**"Permission denied"**
- Ensure read/write access to repository
- Check branch protection rules

**"Too aggressive cleanup"**
- Use `--conservative` flag to raise threshold
- Adjust `stale_threshold_days` in config

**"Agent timeout"**
- Increase timeout in config or use `--timeout <seconds>`
- Large repositories may need extended time

### Debug Mode

```bash
dotnet run -- analyze ~/dev/my-project --verbose --log-level debug
```

Generates detailed logs in `.repoauto/logs/debug-*.log`

---

**For more information, see [Getting Started](getting-started.md) and [Agent Design Guide](agent-design-guide.md).**
