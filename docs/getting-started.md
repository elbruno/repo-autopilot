# Getting Started: 5-Minute Quick Start

Get your first repository cleanup running in under 5 minutes.

## Prerequisites

- Windows 10+, macOS, or Linux
- .NET 8.0+ runtime
- Git installed locally
- Access to your local repositories

## Step 1: Installation (2 minutes)

```bash
# Clone the repository
git clone https://github.com/your-org/local-repo-auto.git
cd local-repo-auto

# Install dependencies
dotnet restore

# Build the project
dotnet build

# Verify installation
dotnet run -- --help
```

You should see the CLI help menu with available commands.

## Step 2: Configure Your Environment (1 minute)

Create a configuration file `.repoauto/config.json`:

```json
{
  "repositories": [
    {
      "path": "~/dev/my-project",
      "name": "my-project",
      "protected_branches": ["main", "develop", "release/*"],
      "stale_threshold_days": 30
    }
  ],
  "safety": {
    "require_approval": true,
    "dry_run_first": true,
    "create_rollback_scripts": true,
    "audit_log": true
  },
  "agents": {
    "enable_semantic_analysis": true,
    "conflict_detection": true,
    "auto_report": true
  }
}
```

## Step 3: Analyze Your First Repository (1 minute)

```bash
# Analyze repository health
dotnet run -- analyze ~/dev/my-project

# Output:
# ╔═══════════════════════════════════════╗
# ║   Repository Analysis Report          ║
# ║   ~/dev/my-project                    ║
# ╚═══════════════════════════════════════╝
# 
# Total Branches: 47
# ├─ Active (recent commits): 22
# ├─ Stale (>30 days): 18
# ├─ Protected: 4
# └─ Orphaned: 3
#
# Merge Conflicts: 2 detected
# Unresolved: branch-x vs. branch-y
#
# Technical Debt:
# ├─ Dead Code Detected: src/legacy-auth.cs
# └─ Unused Dependencies: 3
```

## Step 4: Your First Cleanup (1 minute)

```bash
# Run cleanup with dry-run (safe preview)
dotnet run -- cleanup ~/dev/my-project --dry-run

# Output shows what WOULD be deleted:
# ┌─ Would Delete (15 branches):
# │  • feature/old-dashboard (last commit: 2025-09-10)
# │  • hotfix/temp-fix (last commit: 2025-08-22)
# │  • refactor/deprecated (last commit: 2025-07-15)
# │  ... (12 more)
# │
# └─ Flagged for Review (2 branches):
#    • bugfix/experimental (3 unmerged commits)
#    • refactor/pending (1 unmerged commit)
#
# Ready to proceed? Run without --dry-run to confirm.
```

If the preview looks good:

```bash
# Execute cleanup (requires confirmation)
dotnet run -- cleanup ~/dev/my-project

# Confirm when prompted
# ✓ 15 branches deleted
# ⚠️  2 branches flagged (review required)
# 📄 Audit log: .repoauto/logs/cleanup-2026-03-15.log
# ↩️  Rollback script: .repoauto/rollback/cleanup-2026-03-15.sh
```

## Step 5: Review Results & Next Steps

```bash
# View cleanup report
cat .repoauto/logs/cleanup-2026-03-15.log

# Review flagged branches
git show bugfix/experimental

# If something went wrong, rollback instantly
bash .repoauto/rollback/cleanup-2026-03-15.sh
```

## Next: Customize & Automate

### Run on Schedule

**macOS/Linux:**
```bash
# Setup nightly cleanup (runs at 2am)
echo "0 2 * * * cd ~/dev && dotnet run -- cleanup ~/dev/my-project" | crontab -
```

**Windows (PowerShell):**
```powershell
# Create scheduled task
Register-ScheduledJob -Name "RepoCleanup" -ScriptBlock {
  cd C:\src\localRepoAuto
  dotnet run -- cleanup C:\dev\my-project
} -Trigger (New-JobTrigger -Daily -At 2:00AM)
```

### Multi-Repository Setup

```bash
# Cleanup all configured repositories at once
dotnet run -- cleanup-all

# Generates combined report
# 📊 Cleanup Report: All Repositories
# ├─ my-project: 15 branches deleted
# ├─ team-api: 8 branches deleted
# └─ shared-lib: 3 branches deleted
# 
# Total: 26 branches cleaned, 2 hours reclaimed
```

### Advanced Configuration

See [API Reference](api-reference.md) for complete configuration options including:
- Custom branch patterns
- Conflict detection strategies
- Rollback retention policies
- Logging and audit trails
- Semantic analysis tuning

## Troubleshooting

**"Repository not found"**
- Verify path in config.json points to a valid Git repository
- Use absolute paths; ~ expansion may not work in config file

**"Permission denied" when deleting branches**
- Ensure you have write access to the repository
- Check for protected branch rules in Git configuration

**"Too many branches deleted"**
- Use `--conservative` flag to raise staleness threshold
- Review flagged branches before approval

**Want to undo?**
```bash
# Every cleanup creates a rollback script
bash .repoauto/rollback/cleanup-2026-03-15.sh
```

## Next Steps

1. **Explore the [Agent Design Guide](agent-design-guide.md)** to understand how agents work
2. **Check [API Reference](api-reference.md)** for advanced configuration
3. **Read [Case Studies](case-studies.md)** to see real-world examples
4. **Review the [Blog Post](blog-post-main.md)** for the full vision

---

**Questions?** Check the [API Reference](api-reference.md) or open an issue on GitHub.
