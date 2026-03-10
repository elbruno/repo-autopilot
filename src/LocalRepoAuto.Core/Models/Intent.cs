namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Represents a parsed developer intent.
/// Extracted from natural language input into structured form.
/// </summary>
public class Intent
{
    /// <summary>
    /// The type of intent (delete-stale-branches, resolve-conflicts, etc).
    /// </summary>
    public IntentType Type { get; set; }

    /// <summary>
    /// Extracted parameters (threshold_days, target_branches, merge_status, etc).
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Confidence level 0-1. How sure we are about the parse.
    /// 1.0 = very clear (e.g., "delete merged branches older than 90 days")
    /// 0.5 = ambiguous (e.g., "clean up old stuff")
    /// </summary>
    public double ConfidenceLevel { get; set; } = 1.0;

    /// <summary>
    /// Original user input before parsing.
    /// </summary>
    public string RawInput { get; set; } = string.Empty;

    /// <summary>
    /// When this intent was parsed.
    /// </summary>
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Human-readable summary of the parsed intent.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Get a parameter by key, returning default if not found.
    /// </summary>
    public T? GetParameter<T>(string key, T? defaultValue = default)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            return value is T typedValue ? typedValue : defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Set a parameter value.
    /// </summary>
    public void SetParameter(string key, object value)
    {
        Parameters[key] = value;
    }
}

/// <summary>
/// All supported intent types.
/// </summary>
public enum IntentType
{
    /// <summary>
    /// Delete branches that are old and no longer needed.
    /// Parameters: threshold_days, merge_status (optional), excluded_branches, author (optional)
    /// </summary>
    DeleteStaleBranches,

    /// <summary>
    /// Resolve merge conflicts automatically or with suggestions.
    /// Parameters: target_branches (optional), strategy (optional)
    /// </summary>
    ResolveConflicts,

    /// <summary>
    /// Merge one branch into another.
    /// Parameters: source_branch, target_branch, strategy (optional)
    /// </summary>
    MergeBranches,

    /// <summary>
    /// Analyze repository and get detailed branch inventory.
    /// Parameters: none
    /// </summary>
    AnalyzeRepository,

    /// <summary>
    /// Check repository health: branches, conflicts, recommendations.
    /// Parameters: none
    /// </summary>
    CheckHealth,

    /// <summary>
    /// Validate a specific branch for safety/viability.
    /// Parameters: branch_name, check_type (optional)
    /// </summary>
    ValidateBranch,

    /// <summary>
    /// List all branches with their metadata.
    /// Parameters: filter (optional: stale, merged, protected, etc)
    /// </summary>
    ListBranches,

    /// <summary>
    /// Cleanup remote tracking branches.
    /// Parameters: threshold_days (optional)
    /// </summary>
    CleanupRemoteBranches,

    /// <summary>
    /// Auto-merge branches with no conflicts.
    /// Parameters: source_branch, target_branch
    /// </summary>
    AutoMergeSimple,

    /// <summary>
    /// Force resolve complex conflicts (use with caution).
    /// Parameters: strategy (ours, theirs, manual), target_branch
    /// </summary>
    ForceResolveConflict,

    /// <summary>
    /// Resume an interrupted workflow from a checkpoint.
    /// Parameters: checkpoint_id
    /// </summary>
    ResumeWorkflow
}
