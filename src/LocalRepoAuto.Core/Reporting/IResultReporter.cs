using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Reporting;

/// <summary>
/// Result reporter interface: generate human-readable reports from workflow outcomes.
/// </summary>
public interface IResultReporter
{
    /// <summary>
    /// Generate a cleanup report summarizing deleted branches.
    /// Includes: branches deleted, storage reclaimed, time saved, recommendations.
    /// </summary>
    Task<string> GenerateCleanupReportAsync(WorkflowOutcome outcome);

    /// <summary>
    /// Generate a conflict resolution report.
    /// Includes: conflicts resolved, strategies used, manual review needed.
    /// </summary>
    Task<string> GenerateConflictReportAsync(WorkflowOutcome outcome);

    /// <summary>
    /// Generate a repository health report.
    /// Includes: repository statistics, recommendations, health score.
    /// </summary>
    Task<string> GenerateHealthReportAsync(WorkflowOutcome outcome);

    /// <summary>
    /// Export report in different formats (markdown, json, html, plaintext).
    /// </summary>
    Task<string> ExportReportAsync(string reportContent, string format = "markdown");
}
