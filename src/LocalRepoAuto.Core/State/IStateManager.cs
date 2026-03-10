using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.State;

/// <summary>
/// State manager interface: save/load workflow checkpoints for resumable workflows.
/// </summary>
public interface IStateManager
{
    /// <summary>
    /// Save a workflow checkpoint before executing major actions.
    /// Enables resumption if workflow is interrupted.
    /// </summary>
    Task<string> SaveCheckpointAsync(WorkflowCheckpoint checkpoint);

    /// <summary>
    /// Load a checkpoint by ID.
    /// Returns null if checkpoint not found.
    /// </summary>
    Task<WorkflowCheckpoint?> LoadCheckpointAsync(string checkpointId);

    /// <summary>
    /// List all checkpoints for a given workflow.
    /// </summary>
    Task<List<WorkflowCheckpoint>> ListCheckpointsAsync(string workflowId);

    /// <summary>
    /// Delete a checkpoint after successful completion.
    /// Keeps storage clean.
    /// </summary>
    Task DeleteCheckpointAsync(string checkpointId);

    /// <summary>
    /// Get the checkpoint directory path.
    /// </summary>
    string GetCheckpointDirectory();

    /// <summary>
    /// Get file path for a specific checkpoint.
    /// </summary>
    string GetCheckpointFilePath(string checkpointId);
}
