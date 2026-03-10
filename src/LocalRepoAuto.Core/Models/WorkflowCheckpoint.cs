namespace LocalRepoAuto.Core.Models;

/// <summary>
/// A checkpoint saved during workflow execution.
/// Allows resumption if workflow is interrupted.
/// </summary>
public class WorkflowCheckpoint
{
    /// <summary>
    /// Unique identifier for this checkpoint.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Which workflow this checkpoint belongs to.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label describing this checkpoint.
    /// Examples: "branches-listed", "candidates-filtered", "proposal-generated", "execution-step-3"
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The complete workflow state at this checkpoint.
    /// Can be serialized to/from JSON.
    /// </summary>
    public Dictionary<string, object> State { get; set; } = new();

    /// <summary>
    /// When this checkpoint was saved.
    /// </summary>
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Status of this checkpoint: "Active", "Completed", "Abandoned", "Failed"
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Repository path this checkpoint was created for.
    /// </summary>
    public string RepoPath { get; set; } = string.Empty;

    /// <summary>
    /// The original intent being executed.
    /// </summary>
    public Intent? OriginalIntent { get; set; }

    /// <summary>
    /// Which step in the workflow this checkpoint represents (0-indexed).
    /// </summary>
    public int StepNumber { get; set; } = 0;

    /// <summary>
    /// Human-readable description of this checkpoint.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Get a state value by key.
    /// </summary>
    public T? GetState<T>(string key, T? defaultValue = default)
    {
        if (State.TryGetValue(key, out var value))
        {
            return value is T typedValue ? typedValue : defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Set a state value.
    /// </summary>
    public void SetState(string key, object value)
    {
        State[key] = value;
    }
}
