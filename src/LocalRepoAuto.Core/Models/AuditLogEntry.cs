namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Audit log entry for a single operation in a workflow.
/// Immutable, append-only, used for compliance and debugging.
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Which workflow this entry belongs to.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// When this operation occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who/what performed the action (agent name or "User").
    /// Examples: "IntentRouter", "BranchAnalyzer", "GitOperations", "ConflictResolver"
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// What action was performed.
    /// Examples: "ParseIntent", "ListBranches", "DeleteBranch", "ResolveConflict"
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Input parameters to the action.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Output/results from the action.
    /// </summary>
    public Dictionary<string, object> Results { get; set; } = new();

    /// <summary>
    /// Status of the action: "Started", "Completed", "Failed", "Skipped"
    /// </summary>
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// If Status is "Failed", this contains the error message.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether this action can be rolled back (e.g., branch deletion can, merge might not).
    /// </summary>
    public bool IsReversible { get; set; } = false;

    /// <summary>
    /// Information needed to rollback this action (e.g., branch hash before deletion).
    /// Only populated if IsReversible is true.
    /// </summary>
    public Dictionary<string, object>? RollbackInfo { get; set; }

    /// <summary>
    /// How long the action took (milliseconds).
    /// </summary>
    public int DurationMs { get; set; } = 0;

    /// <summary>
    /// Operator name (if user-initiated) or "system" (if automated).
    /// </summary>
    public string Operator { get; set; } = "system";

    /// <summary>
    /// Human-readable description of what happened.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
