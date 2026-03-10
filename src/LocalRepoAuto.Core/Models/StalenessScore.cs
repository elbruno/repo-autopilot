namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Staleness score for a branch with component breakdown.
/// </summary>
public class StalenessScore
{
    /// <summary>
    /// Overall staleness score (0-100). Higher = more stale.
    /// Protected branches always score 0.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Human-readable explanation of how the score was calculated.
    /// Example: "90 days old (base 100) + 25 wip/ penalty = 100"
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level (0-1) in this staleness assessment.
    /// Lower if author is unknown or inactive.
    /// </summary>
    public double ConfidenceLevel { get; set; } = 1.0;

    /// <summary>
    /// Breakdown of score by component for debugging/understanding.
    /// Keys: "time", "name_pattern", "author_activity", "is_protected"
    /// </summary>
    public Dictionary<string, int> Components { get; set; } = new();

    /// <summary>
    /// Last commit date used for scoring.
    /// </summary>
    public DateTime LastCommitDate { get; set; }

    /// <summary>
    /// Days since last commit.
    /// </summary>
    public int DaysSinceCommit { get; set; }

    /// <summary>
    /// Threshold being applied (in days).
    /// </summary>
    public int ThresholdDays { get; set; } = 30;

    /// <summary>
    /// Whether this branch is protected.
    /// </summary>
    public bool IsProtected { get; set; }
}

/// <summary>
/// Result of a merge operation (dry-run or actual).
/// </summary>
public class MergeResult
{
    /// <summary>
    /// True if merge succeeded without conflicts.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// List of conflicts detected during merge.
    /// Empty if Success is true.
    /// </summary>
    public List<ConflictInfo> Conflicts { get; set; } = new();

    /// <summary>
    /// Commit hash of the merge commit (if successful).
    /// </summary>
    public string? MergeCommitHash { get; set; }

    /// <summary>
    /// Error message if merge failed.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Number of files changed.
    /// </summary>
    public int FilesChanged { get; set; }

    /// <summary>
    /// Number of insertions.
    /// </summary>
    public int Insertions { get; set; }

    /// <summary>
    /// Number of deletions.
    /// </summary>
    public int Deletions { get; set; }

    /// <summary>
    /// Whether this was a dry-run (no actual merge).
    /// </summary>
    public bool IsDryRun { get; set; } = true;
}
