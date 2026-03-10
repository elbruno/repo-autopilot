using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Interface for analyzing branches and their staleness.
/// Provides comprehensive branch inventory with staleness scoring.
/// </summary>
public interface IBranchAnalyzer
{
    /// <summary>
    /// Lists all branches in the repository with metadata.
    /// </summary>
    /// <returns>List of all branches with commit dates, authors, and initial staleness analysis</returns>
    Task<List<BranchInfo>> ListBranchesAsync();

    /// <summary>
    /// Gets detailed metadata for a specific branch.
    /// </summary>
    /// <param name="branchName">Name of the branch to analyze</param>
    /// <returns>Detailed branch information including staleness score</returns>
    /// <exception cref="InvalidRefException">Thrown when branch doesn't exist</exception>
    Task<BranchInfo> GetBranchMetadataAsync(string branchName);

    /// <summary>
    /// Identifies all stale branches based on configured threshold.
    /// Excludes protected branches (main, master, develop, etc).
    /// </summary>
    /// <param name="thresholdDays">Staleness threshold in days (default: 30)</param>
    /// <returns>List of branches meeting staleness criteria, sorted by staleness score (highest first)</returns>
    Task<List<BranchInfo>> DetectStaleBranchesAsync(int? thresholdDays = null);

    /// <summary>
    /// Gets detailed analysis of why a branch is considered stale.
    /// </summary>
    /// <param name="branchName">Branch to analyze</param>
    /// <returns>Staleness reasoning with component scores and confidence</returns>
    Task<StalenessScore> AnalyzeStalenessAsync(string branchName);
}
