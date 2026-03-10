using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Interface for detecting merge conflicts between branches.
/// Analyzes complexity and suggests resolution strategies.
/// </summary>
public interface IConflictDetector
{
    /// <summary>
    /// Detects all merge conflicts between two references without modifying the working tree.
    /// Uses a dry-run merge on a detached HEAD.
    /// </summary>
    /// <param name="baseBranch">Base reference (merge target)</param>
    /// <param name="headBranch">Head reference (branch to merge)</param>
    /// <returns>List of detected conflicts with complexity analysis</returns>
    Task<List<ConflictInfo>> DetectConflictsAsync(string baseBranch, string headBranch);

    /// <summary>
    /// Analyzes the complexity of a specific conflict region.
    /// </summary>
    /// <param name="filePath">Path to the conflicted file</param>
    /// <param name="conflictContent">The conflict marker content (<<<<<<< ... =======  ... >>>>>>>)</param>
    /// <returns>Complexity classification and resolution suggestions</returns>
    Task<ConflictComplexity> AnalyzeConflictComplexityAsync(string filePath, string conflictContent);

    /// <summary>
    /// Performs a dry-run merge and captures all conflicts without affecting working tree.
    /// </summary>
    /// <param name="baseBranch">Merge target</param>
    /// <param name="headBranch">Branch to merge</param>
    /// <returns>Detailed merge result with conflict information</returns>
    Task<MergeResult> PerformDryRunMergeAsync(string baseBranch, string headBranch);
}

/// <summary>
/// Categorizes conflict complexity for resolution guidance.
/// </summary>
public enum ConflictComplexity
{
    /// <summary>
    /// Whitespace-only conflicts or auto-resolvable changes.
    /// Score: 0-30
    /// </summary>
    Simple = 10,

    /// <summary>
    /// Non-overlapping line ranges with both sides having real changes.
    /// Score: 30-70
    /// </summary>
    Medium = 50,

    /// <summary>
    /// Semantic conflicts - same variable/method modified, deletion vs modification, etc.
    /// Score: 70-100
    /// </summary>
    Complex = 80
}
