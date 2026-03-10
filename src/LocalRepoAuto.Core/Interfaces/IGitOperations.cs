using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Safe wrapper interface for Git operations.
/// All operations are local-only and include comprehensive logging.
/// </summary>
public interface IGitOperations
{
    /// <summary>
    /// Lists all local and remote branches in the repository.
    /// </summary>
    /// <returns>List of branch names with remote prefixes (e.g., "origin/main")</returns>
    Task<List<string>> GetBranchesAsync();

    /// <summary>
    /// Retrieves commit metadata for a given reference (branch, tag, or commit hash).
    /// </summary>
    /// <param name="reference">Git reference (branch name, tag, or commit hash)</param>
    /// <returns>Commit metadata including author, date, and message</returns>
    /// <exception cref="InvalidRefException">Thrown when reference doesn't exist</exception>
    Task<CommitMetadata> GetCommitMetadataAsync(string reference);

    /// <summary>
    /// Gets the current branch name. Returns "(HEAD detached)" if in detached state.
    /// </summary>
    Task<string> GetCurrentBranchAsync();

    /// <summary>
    /// Computes diff statistics between two references.
    /// </summary>
    /// <param name="baseBranch">Base reference for comparison</param>
    /// <param name="headBranch">Head reference for comparison</param>
    /// <returns>Diff statistics including file counts and line changes</returns>
    Task<DiffResult> GetDiffAsync(string baseBranch, string headBranch);

    /// <summary>
    /// Gets detailed diff statistics (insertions, deletions by file).
    /// </summary>
    /// <param name="baseBranch">Base reference</param>
    /// <param name="headBranch">Head reference</param>
    /// <returns>Detailed statistics per file</returns>
    Task<DiffStatResult> GetDiffStatAsync(string baseBranch, string headBranch);

    /// <summary>
    /// Finds the common ancestor commit between two references.
    /// </summary>
    /// <param name="ref1">First reference</param>
    /// <param name="ref2">Second reference</param>
    /// <returns>Commit hash of common ancestor</returns>
    Task<string> GetCommonAncestorAsync(string ref1, string ref2);

    /// <summary>
    /// Checks if a branch can be safely deleted without losing unmerged commits.
    /// </summary>
    /// <param name="branchName">Branch to check</param>
    /// <returns>True if branch can be deleted, false if it has unmerged commits</returns>
    Task<bool> CanDeleteBranchAsync(string branchName);

    /// <summary>
    /// Deletes a branch (local or remote).
    /// </summary>
    /// <param name="branchName">Branch to delete (e.g., "feature/old-feature" or "origin/stale")</param>
    /// <param name="force">If true, deletes even if unmerged; use with caution</param>
    /// <exception cref="PermissionDeniedException">Thrown if branch is protected</exception>
    Task DeleteBranchAsync(string branchName, bool force = false);

    /// <summary>
    /// Attempts to merge a branch into another with given merge strategy.
    /// </summary>
    /// <param name="baseBranch">Target branch to merge into</param>
    /// <param name="headBranch">Branch to merge from</param>
    /// <param name="strategy">Merge strategy (e.g., "recursive", "resolve")</param>
    /// <returns>Merge result with conflict information if applicable</returns>
    Task<MergeResult> MergeBranchesAsync(string baseBranch, string headBranch, string strategy = "recursive");

    /// <summary>
    /// Gets all operations logged since agent initialization.
    /// </summary>
    /// <returns>List of all Git operation logs</returns>
    List<GitOperationLog> GetOperationLogs();

    /// <summary>
    /// Clears all operation logs.
    /// </summary>
    void ClearOperationLogs();
}
