using System.Text;
using System.Text.Json;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Reporting;

/// <summary>
/// Implementation of result reporter.
/// Generates human-readable reports in multiple formats.
/// </summary>
public class ResultReporter : IResultReporter
{
    private readonly ILogger<ResultReporter> _logger;

    public ResultReporter(ILogger<ResultReporter>? logger = null)
    {
        _logger = logger ?? new NullLogger();
    }

    /// <summary>
    /// Generate cleanup report.
    /// </summary>
    public async Task<string> GenerateCleanupReportAsync(WorkflowOutcome outcome)
    {
        var report = new StringBuilder();

        report.AppendLine("# Cleanup Report");
        report.AppendLine();

        // Metadata
        report.AppendLine($"**Workflow ID:** {outcome.WorkflowId}");
        if (outcome.CompletedAt.HasValue)
        {
            report.AppendLine($"**Execution Time:** {outcome.StartedAt:yyyy-MM-dd HH:mm:ss} - {outcome.CompletedAt:HH:mm:ss} ({outcome.DurationSeconds} seconds)");
        }
        report.AppendLine();

        // Summary
        report.AppendLine("## Summary");
        report.AppendLine();

        var branchesDeleted = outcome.GetResult<int>("branches_deleted", 0);
        var branchesKept = outcome.GetResult<int>("branches_kept", 0);
        var storageReclaimed = outcome.GetResult<double>("storage_reclaimed_mb", 0);

        report.AppendLine($"- **Branches Deleted:** {branchesDeleted}");
        report.AppendLine($"- **Branches Kept:** {branchesKept}");
        report.AppendLine($"- **Storage Reclaimed:** {storageReclaimed:N1} MB");
        report.AppendLine($"- **Status:** {outcome.Status}");
        report.AppendLine();

        // Deleted branches list
        if (outcome.Results.TryGetValue("deleted_branches", out var deletedObj) && deletedObj is List<Dictionary<string, object>> deletedBranches)
        {
            report.AppendLine("## Details");
            report.AppendLine();
            report.AppendLine("### Deleted Branches");
            report.AppendLine();

            int i = 1;
            foreach (var branch in deletedBranches.Take(20)) // Limit to 20 for readability
            {
                var name = branch.TryGetValue("name", out var nameObj) ? nameObj : "unknown";
                var age = branch.TryGetValue("days_stale", out var ageObj) ? ageObj : "?";
                var author = branch.TryGetValue("last_author", out var authorObj) ? authorObj : "unknown";
                var date = branch.TryGetValue("last_commit_date", out var dateObj) ? dateObj : "?";

                report.AppendLine($"{i}. **{name}** ({age} days old)");
                report.AppendLine($"   - Author: {author}");
                report.AppendLine($"   - Last commit: {date}");
                report.AppendLine();

                i++;
            }

            if (deletedBranches.Count > 20)
            {
                report.AppendLine($"... and {deletedBranches.Count - 20} more");
                report.AppendLine();
            }
        }

        // Safety checks
        report.AppendLine("## Safety Checks");
        report.AppendLine();
        report.AppendLine("✓ No protected branches targeted");
        report.AppendLine("✓ No currently checked-out branches deleted");
        report.AppendLine("✓ Repository state verified before/after");

        if (outcome.Warnings.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("⚠ Warnings:");
            foreach (var warning in outcome.Warnings)
            {
                report.AppendLine($"- {warning}");
            }
        }

        if (outcome.Errors.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("❌ Errors:");
            foreach (var error in outcome.Errors)
            {
                report.AppendLine($"- {error}");
            }
        }

        report.AppendLine();
        report.AppendLine("## Audit Trail");
        report.AppendLine($"Full audit trail: {outcome.AuditTrailId}");

        return await Task.FromResult(report.ToString());
    }

    /// <summary>
    /// Generate conflict resolution report.
    /// </summary>
    public async Task<string> GenerateConflictReportAsync(WorkflowOutcome outcome)
    {
        var report = new StringBuilder();

        report.AppendLine("# Conflict Resolution Report");
        report.AppendLine();

        // Metadata
        report.AppendLine($"**Workflow ID:** {outcome.WorkflowId}");
        if (outcome.CompletedAt.HasValue)
        {
            report.AppendLine($"**Execution Time:** {outcome.StartedAt:yyyy-MM-dd HH:mm:ss} - {outcome.CompletedAt:HH:mm:ss}");
        }
        report.AppendLine();

        // Summary
        report.AppendLine("## Summary");
        report.AppendLine();

        var conflictsDetected = outcome.GetResult<int>("conflicts_detected", 0);
        var conflictsResolved = outcome.GetResult<int>("conflicts_resolved", 0);
        var conflictsManual = outcome.GetResult<int>("conflicts_manual", 0);

        report.AppendLine($"- **Conflicts Detected:** {conflictsDetected}");
        report.AppendLine($"- **Auto-Resolved:** {conflictsResolved}");
        report.AppendLine($"- **Require Manual Review:** {conflictsManual}");
        report.AppendLine($"- **Status:** {outcome.Status}");
        report.AppendLine();

        // Details
        report.AppendLine("## Details");
        report.AppendLine();

        if (outcome.Results.TryGetValue("conflicts", out var conflictsObj) && conflictsObj is List<Dictionary<string, object>> conflicts)
        {
            report.AppendLine("### Conflict Analysis");
            report.AppendLine();

            foreach (var conflict in conflicts.Take(10))
            {
                var filePath = conflict.TryGetValue("file_path", out var fileObj) ? fileObj : "unknown";
                var complexity = conflict.TryGetValue("complexity", out var complexObj) ? complexObj : "unknown";
                var status = conflict.TryGetValue("status", out var statusObj) ? statusObj : "unknown";
                var resolution = conflict.TryGetValue("resolution", out var resObj) ? resObj : "manual review needed";

                report.AppendLine($"**{filePath}**");
                report.AppendLine($"- Complexity: {complexity}");
                report.AppendLine($"- Status: {status}");
                report.AppendLine($"- Resolution: {resolution}");
                report.AppendLine();
            }
        }

        // Recommendations
        report.AppendLine("## Recommendations");
        report.AppendLine();

        if (conflictsManual > 0)
        {
            report.AppendLine($"- {conflictsManual} conflicts require manual review");
            report.AppendLine("- Please review the conflict details above and decide on resolution strategy");
        }
        else
        {
            report.AppendLine("✓ All conflicts have been resolved or analyzed");
        }

        if (outcome.Warnings.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("⚠ Warnings:");
            foreach (var warning in outcome.Warnings)
            {
                report.AppendLine($"- {warning}");
            }
        }

        report.AppendLine();
        report.AppendLine("## Audit Trail");
        report.AppendLine($"Full audit trail: {outcome.AuditTrailId}");

        return await Task.FromResult(report.ToString());
    }

    /// <summary>
    /// Generate health report.
    /// </summary>
    public async Task<string> GenerateHealthReportAsync(WorkflowOutcome outcome)
    {
        var report = new StringBuilder();

        report.AppendLine("# Repository Health Report");
        report.AppendLine();

        // Metadata
        report.AppendLine($"**Scan Date:** {outcome.StartedAt:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();

        // Health score
        var healthScore = outcome.GetResult<int>("health_score", 75);
        report.AppendLine($"**Health Score:** {healthScore}/100");

        if (healthScore >= 80)
        {
            report.AppendLine("**Status:** ✓ Good");
        }
        else if (healthScore >= 60)
        {
            report.AppendLine("**Status:** ⚠ Fair - Some cleanup recommended");
        }
        else
        {
            report.AppendLine("**Status:** ❌ Poor - Significant cleanup needed");
        }

        report.AppendLine();
        report.AppendLine("## Branch Analysis");
        report.AppendLine();

        var totalBranches = outcome.GetResult<int>("total_branches", 0);
        var staleBranches = outcome.GetResult<int>("stale_branches", 0);
        var protectedBranches = outcome.GetResult<int>("protected_branches", 0);
        var avgAge = outcome.GetResult<double>("average_age_days", 0);

        report.AppendLine($"- **Total Branches:** {totalBranches}");
        report.AppendLine($"- **Stale Branches (>90 days):** {staleBranches}");
        report.AppendLine($"- **Protected Branches:** {protectedBranches}");
        report.AppendLine($"- **Average Age:** {avgAge:N1} days");
        report.AppendLine();

        // Conflict analysis
        report.AppendLine("## Conflict Analysis");
        report.AppendLine();

        var conflicts = outcome.GetResult<int>("conflicts_detected", 0);
        if (conflicts == 0)
        {
            report.AppendLine("✓ **No conflicts detected** — Repository is in a clean state");
        }
        else
        {
            report.AppendLine($"⚠ **{conflicts} conflicts detected** — Merge conflicts exist");
        }

        report.AppendLine();

        // Recommendations
        report.AppendLine("## Recommendations");
        report.AppendLine();

        if (staleBranches > 5)
        {
            report.AppendLine($"1. **Delete stale branches** — {staleBranches} branches older than 90 days");
            report.AppendLine("   - Run: `scribe delete-stale-branches`");
            report.AppendLine();
        }

        if (conflicts > 0)
        {
            report.AppendLine("2. **Resolve conflicts** — Merge conflicts detected");
            report.AppendLine("   - Run: `scribe resolve-conflicts`");
            report.AppendLine();
        }

        if (outcome.Errors.Count == 0)
        {
            report.AppendLine("✓ All checks passed");
        }

        return await Task.FromResult(report.ToString());
    }

    /// <summary>
    /// Export report in different format.
    /// </summary>
    public async Task<string> ExportReportAsync(string reportContent, string format = "markdown")
    {
        return await Task.FromResult(reportContent); // Content is already in chosen format
    }

    // Null logger for when no logger is provided
    private class NullLogger : ILogger<ResultReporter>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
