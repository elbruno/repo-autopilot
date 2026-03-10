using System;
using System.Threading.Tasks;

namespace LocalRepoAuto.Core.Safety
{
    /// <summary>
    /// Interface for pre-flight validation of Git operations.
    /// Implementations check safety conditions before destructive operations.
    /// </summary>
    public interface IPreFlightChecker
    {
        /// <summary>
        /// Validate a developer intent for safety and clarity.
        /// Checks: parsed correctly, parameters valid, no contradictions.
        /// </summary>
        Task<PreFlightResult> ValidateIntentAsync(string intent, string repoPath);

        /// <summary>
        /// Validate that a branch is safe to delete.
        /// Checks: not current branch, not protected, fully merged, no unique commits.
        /// </summary>
        Task<PreFlightResult> ValidateBranchDeletionAsync(string branchName, string repoPath);

        /// <summary>
        /// Validate that a merge operation is safe to execute.
        /// Checks: merge-able, no conflicts, target not protected (for force merge).
        /// </summary>
        Task<PreFlightResult> ValidateMergeAsync(string baseBranch, string headBranch, string repoPath);

        /// <summary>
        /// Check overall repository health and state before any operation.
        /// Checks: not in detached HEAD, working dir clean, index valid, locks absent.
        /// </summary>
        Task<PreFlightResult> CheckRepositoryStateAsync(string repoPath);

        /// <summary>
        /// Validate conflict resolution is safe for a specific file.
        /// Checks: conflict type, file criticality, Copilot validation.
        /// </summary>
        Task<PreFlightResult> ValidateConflictResolutionAsync(string filePath, string repoPath);

        /// <summary>
        /// Validate user configuration is valid before executing operations.
        /// Checks: JSON valid, protected branches list sensible, thresholds reasonable.
        /// </summary>
        Task<PreFlightResult> ValidateConfigurationAsync(string configPath);

        /// <summary>
        /// Check if there are sufficient permissions and system resources.
        /// Checks: write permissions to .git, disk space, Git version.
        /// </summary>
        Task<PreFlightResult> CheckSystemCapabilitiesAsync(string repoPath);
    }
}
