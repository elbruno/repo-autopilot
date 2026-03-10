namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Complete information about a Git branch.
/// </summary>
public class BranchInfo
{
    /// <summary>
    /// Branch name (e.g., "main", "feature/auth", "origin/develop").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short commit hash of the last commit on this branch.
    /// </summary>
    public string LastCommitHash { get; set; } = string.Empty;

    /// <summary>
    /// Full commit hash of the last commit on this branch.
    /// </summary>
    public string LastCommitHashFull { get; set; } = string.Empty;

    /// <summary>
    /// Date and time of the last commit on this branch.
    /// </summary>
    public DateTime LastCommitDate { get; set; }

    /// <summary>
    /// Name of the author who made the last commit.
    /// </summary>
    public string LastAuthor { get; set; } = string.Empty;

    /// <summary>
    /// Email of the author who made the last commit.
    /// </summary>
    public string LastAuthorEmail { get; set; } = string.Empty;

    /// <summary>
    /// Subject line of the last commit message.
    /// </summary>
    public string LastCommitMessage { get; set; } = string.Empty;

    /// <summary>
    /// True if this branch exists only locally (not pushed to any remote).
    /// </summary>
    public bool IsLocalOnly { get; set; }

    /// <summary>
    /// True if this branch is protected and should never be deleted/modified.
    /// Examples: main, master, develop, production.
    /// </summary>
    public bool IsProtected { get; set; }

    /// <summary>
    /// Staleness score from 0-100. Higher = more stale.
    /// Protected branches always have score 0.
    /// </summary>
    public int StalenessScore { get; set; }

    /// <summary>
    /// Human-readable explanation of the staleness score.
    /// Example: "90 days old (wip/ prefix adds 25 points)"
    /// </summary>
    public string StalenessReason { get; set; } = string.Empty;

    /// <summary>
    /// Number of days since last commit on this branch.
    /// </summary>
    public int DaysStale { get; set; }

    /// <summary>
    /// Confidence level (0-1) in the staleness score.
    /// Lower confidence if author is inactive or unknown.
    /// </summary>
    public double StalenessConfidence { get; set; } = 1.0;

    /// <summary>
    /// Whether this is the current branch (working tree checked out).
    /// </summary>
    public bool IsCurrentBranch { get; set; }

    /// <summary>
    /// True if this branch is in detached HEAD state.
    /// </summary>
    public bool IsDetachedHead { get; set; }

    /// <summary>
    /// Merge base commit hash if available (for tracking relationships).
    /// </summary>
    public string? MergeBaseHash { get; set; }

    /// <summary>
    /// Commits ahead of main branch (if applicable).
    /// </summary>
    public int CommitsAhead { get; set; }

    /// <summary>
    /// Commits behind main branch (if applicable).
    /// </summary>
    public int CommitsBehind { get; set; }
}
