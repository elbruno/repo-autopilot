using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Interface for Copilot SDK integration.
/// Provides semantic analysis of conflicts and resolution suggestions.
/// Gracefully degrades if SDK is unavailable.
/// </summary>
public interface ICopilotService
{
    /// <summary>
    /// Analyze a diff to identify conflict regions and extract semantic context.
    /// Uses Copilot SDK if available, falls back to heuristics otherwise.
    /// </summary>
    /// <param name="baseBranch">Base/target branch</param>
    /// <param name="headBranch">Head branch being merged</param>
    /// <param name="filePath">Path to the file with conflict</param>
    /// <param name="baseContent">File content from base branch</param>
    /// <param name="headContent">File content from head branch</param>
    /// <returns>Structured conflict context with semantic understanding</returns>
    Task<ConflictContext> AnalyzeDiffAsync(
        string baseBranch,
        string headBranch,
        string filePath,
        string baseContent,
        string headContent);

    /// <summary>
    /// Suggest safe resolutions for a conflict region using semantic understanding.
    /// Returns 1-3 proposals ranked by confidence.
    /// </summary>
    /// <param name="filePath">Path to the conflicted file</param>
    /// <param name="conflict">Conflict marker information</param>
    /// <param name="context">Additional context (surrounding code, etc)</param>
    /// <returns>List of resolution proposals, highest confidence first</returns>
    Task<List<ConflictProposal>> SuggestResolutionAsync(
        string filePath,
        ConflictMarker conflict,
        string context);

    /// <summary>
    /// Classify the type of conflict for strategy selection.
    /// </summary>
    /// <param name="conflict">Conflict information to classify</param>
    /// <returns>Semantic classification of the conflict</returns>
    Task<SemanticConflictType> ClassifyConflictAsync(ConflictInfo conflict);

    /// <summary>
    /// Check if Copilot SDK is available and responsive.
    /// </summary>
    /// <returns>True if SDK is available, false otherwise</returns>
    Task<bool> IsAvailableAsync();
}
