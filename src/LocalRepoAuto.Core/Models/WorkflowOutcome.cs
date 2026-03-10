namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Complete outcome from a workflow execution.
/// Aggregates results from all steps, including decisions and actions.
/// </summary>
public class WorkflowOutcome
{
    /// <summary>
    /// Unique identifier for this workflow execution.
    /// </summary>
    public string WorkflowId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The intent that triggered this workflow.
    /// </summary>
    public Intent? OriginalIntent { get; set; }

    /// <summary>
    /// Overall status of the workflow.
    /// </summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.InProgress;

    /// <summary>
    /// All steps executed in this workflow.
    /// </summary>
    public List<WorkflowStep> Steps { get; set; } = new();

    /// <summary>
    /// Aggregated results from all steps.
    /// </summary>
    public Dictionary<string, object> Results { get; set; } = new();

    /// <summary>
    /// Any errors encountered during execution.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Any warnings during execution (non-fatal).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// When the workflow started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the workflow completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total execution time in seconds (if completed).
    /// </summary>
    public int? DurationSeconds => CompletedAt.HasValue 
        ? (int)(CompletedAt.Value - StartedAt).TotalSeconds 
        : null;

    /// <summary>
    /// Link to full audit trail for this workflow.
    /// Path to audit log file.
    /// </summary>
    public string AuditTrailId { get; set; } = string.Empty;

    /// <summary>
    /// Checkpoint IDs if workflow was resumed.
    /// </summary>
    public List<string> CheckpointIds { get; set; } = new();

    /// <summary>
    /// Repository path this workflow operated on.
    /// </summary>
    public string RepoPath { get; set; } = string.Empty;

    /// <summary>
    /// Get a result value by key.
    /// </summary>
    public T? GetResult<T>(string key, T? defaultValue = default)
    {
        if (Results.TryGetValue(key, out var value))
        {
            return value is T typedValue ? typedValue : defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Set a result value.
    /// </summary>
    public void SetResult(string key, object value)
    {
        Results[key] = value;
    }

    /// <summary>
    /// Add an error and mark workflow as failed if not already.
    /// </summary>
    public void AddError(string error)
    {
        Errors.Add(error);
        if (Status != WorkflowStatus.Failed)
        {
            Status = WorkflowStatus.Failed;
        }
    }

    /// <summary>
    /// Add a warning (non-fatal).
    /// </summary>
    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}

/// <summary>
/// Status of a workflow execution.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// Workflow is currently executing.
    /// </summary>
    InProgress,

    /// <summary>
    /// Workflow completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Workflow failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// Workflow was rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Workflow was paused (awaiting user confirmation, etc).
    /// </summary>
    Paused,

    /// <summary>
    /// Workflow was resumed from a checkpoint.
    /// </summary>
    ResumedFromCheckpoint
}

/// <summary>
/// A single step within a workflow.
/// Tracks agent execution, inputs/outputs, timing, and status.
/// </summary>
public class WorkflowStep
{
    /// <summary>
    /// Which agent executed this step.
    /// Examples: "BranchAnalyzer", "ConflictResolver", "SafetyGuards"
    /// </summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>
    /// What action was performed.
    /// Examples: "ListBranches", "DetectConflicts", "ValidateIntent"
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Input parameters to the step.
    /// </summary>
    public Dictionary<string, object> Input { get; set; } = new();

    /// <summary>
    /// Output from the step.
    /// </summary>
    public Dictionary<string, object> Output { get; set; } = new();

    /// <summary>
    /// When this step was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// How long the step took (milliseconds).
    /// </summary>
    public int DurationMs { get; set; } = 0;

    /// <summary>
    /// Status of this step: "Success", "Warning", "Error", "Skipped"
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Error message if Status is "Error".
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Human-readable description of what this step did.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Get an output value by key.
    /// </summary>
    public T? GetOutput<T>(string key, T? defaultValue = default)
    {
        if (Output.TryGetValue(key, out var value))
        {
            return value is T typedValue ? typedValue : defaultValue;
        }
        return defaultValue;
    }
}

/// <summary>
/// Result from a rollback operation.
/// </summary>
public class RollbackResult
{
    /// <summary>
    /// Whether the rollback succeeded.
    /// </summary>
    public bool Success { get; set; } = false;

    /// <summary>
    /// Which workflow was rolled back.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// How many actions were reversed.
    /// </summary>
    public int ActionsReversed { get; set; } = 0;

    /// <summary>
    /// Any errors encountered during rollback.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Human-readable summary of what was rolled back.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}
