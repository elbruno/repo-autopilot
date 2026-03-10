using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Staleness heuristics engine for calculating staleness scores.
/// Combines time-based, name-based, and author-activity signals.
/// </summary>
public interface IStalenessHeuristics
{
    /// <summary>
    /// Calculates a comprehensive staleness score for a branch.
    /// Combines multiple signals: commit age, branch naming patterns, author activity.
    /// </summary>
    /// <param name="branchInfo">Branch information to analyze</param>
    /// <returns>Staleness score (0-100) with component breakdown and confidence</returns>
    Task<StalenessScore> CalculateStalenessAsync(BranchInfo branchInfo);

    /// <summary>
    /// Determines if a branch is protected and should never be marked stale.
    /// Checks against configured protected branches list.
    /// </summary>
    /// <param name="branchName">Branch name to check</param>
    /// <returns>True if branch is protected (main, master, develop, release/*, etc.)</returns>
    Task<bool> IsProtectedBranchAsync(string branchName);

    /// <summary>
    /// Gets the staleness threshold (in days) from configuration.
    /// </summary>
    int GetThresholdDays();

    /// <summary>
    /// Gets all configured protected branches.
    /// </summary>
    List<string> GetProtectedBranches();
}
