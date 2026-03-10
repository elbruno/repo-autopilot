using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Interface for conflict resolution orchestration.
/// Combines semantic analysis, strategy selection, and proposal generation.
/// </summary>
public interface IConflictResolver
{
    /// <summary>
    /// Attempt to resolve a conflict using the specified merge strategy.
    /// Returns the resolved content with conflict markers removed, or null if unresolvable.
    /// </summary>
    /// <param name="baseContent">File content from merge base</param>
    /// <param name="ourContent">File content from "ours" (current)</param>
    /// <param name="theirContent">File content from "theirs" (incoming)</param>
    /// <param name="strategy">Merge strategy to use</param>
    /// <returns>Resolved content or null if strategy cannot resolve conflict</returns>
    Task<string?> ResolveAsync(
        string baseContent,
        string ourContent,
        string theirContent,
        ResolutionStrategy strategy);

    /// <summary>
    /// Generate 1-3 safe resolution proposals for a conflict.
    /// Proposals are ranked by confidence and include validation results.
    /// </summary>
    /// <param name="request">Resolution request with conflict details</param>
    /// <returns>Resolution response with proposals and audit trail</returns>
    Task<ResolutionResponse> GetResolutionProposalsAsync(ResolutionRequest request);

    /// <summary>
    /// Validate a resolved conflict to ensure syntactic and semantic correctness.
    /// </summary>
    /// <param name="resolvedContent">The proposed resolved content</param>
    /// <param name="filePath">Path to the file (for language-specific validation)</param>
    /// <returns>Validation result with errors/warnings</returns>
    Task<ValidationResult> ValidateResolutionAsync(string resolvedContent, string filePath);
}
