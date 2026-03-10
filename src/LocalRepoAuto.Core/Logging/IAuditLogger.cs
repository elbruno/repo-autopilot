using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Logging;

/// <summary>
/// Audit logger interface: log all operations immutably for compliance and debugging.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Log parsed intent at start of workflow.
    /// </summary>
    Task LogIntentAsync(Intent intent);

    /// <summary>
    /// Log an agent decision or analysis result.
    /// Example: "branch X is stale, marked for deletion"
    /// </summary>
    Task LogDecisionAsync(AuditLogEntry decision);

    /// <summary>
    /// Log a Git action (branch deletion, merge, conflict resolution).
    /// Must be reversible when possible: log before executing, confirm after.
    /// </summary>
    Task LogActionAsync(AuditLogEntry action);

    /// <summary>
    /// Log workflow outcome and final results.
    /// Called after workflow completion.
    /// </summary>
    Task LogOutcomeAsync(AuditLogEntry outcome);

    /// <summary>
    /// Get full audit trail for a workflow.
    /// Returns all entries in chronological order.
    /// </summary>
    Task<List<AuditLogEntry>> GetAuditTrailAsync(string workflowId);

    /// <summary>
    /// Export audit trail in specified format (jsonl, json, csv).
    /// </summary>
    Task<string> ExportAuditTrailAsync(string workflowId, string format = "jsonl");

    /// <summary>
    /// Get audit file path for a workflow.
    /// </summary>
    string GetAuditFilePath(string workflowId);
}
