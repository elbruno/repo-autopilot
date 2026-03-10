using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Workflows;

/// <summary>
/// Workflow orchestrator interface: coordinate multi-step workflows.
/// Manages analysis → decision → execution → reporting pipeline.
/// </summary>
public interface IWorkflowOrchestrator
{
    /// <summary>
    /// Execute full cleanup workflow: analyze → filter → propose → confirm → execute.
    /// Supports resumable checkpoints if previous attempt was interrupted.
    /// </summary>
    Task<WorkflowOutcome> ExecuteCleanupWorkflowAsync(Intent intent, string repoPath);

    /// <summary>
    /// Execute conflict resolution workflow: detect → analyze → propose → execute.
    /// </summary>
    Task<WorkflowOutcome> ExecuteConflictResolutionAsync(Intent intent, string repoPath);

    /// <summary>
    /// Execute repository health check: analyze branches, conflicts, report health.
    /// Read-only, no side effects.
    /// </summary>
    Task<WorkflowOutcome> ExecuteRepositoryHealthAsync(Intent intent, string repoPath);

    /// <summary>
    /// Execute branch listing workflow.
    /// Read-only, no side effects.
    /// </summary>
    Task<WorkflowOutcome> ExecuteListBranchesAsync(Intent intent, string repoPath);

    /// <summary>
    /// Rollback the last executed action.
    /// Requires audit trail with reversible actions logged.
    /// </summary>
    Task<RollbackResult> RollbackLastActionAsync(string repoPath);

    /// <summary>
    /// Resume interrupted workflow from saved checkpoint.
    /// </summary>
    Task<WorkflowOutcome> ResumeFromCheckpointAsync(string checkpointId, string repoPath);
}
