using LocalRepoAuto.Core.Models;

namespace LocalRepoAuto.Core.Interfaces;

/// <summary>
/// Interface for selecting the optimal merge strategy based on conflict type.
/// </summary>
public interface IMergeStrategySelector
{
    /// <summary>
    /// Select the optimal merge strategy for a specific conflict.
    /// Considers conflict type, complexity, and safety constraints.
    /// </summary>
    /// <param name="conflict">Conflict information including complexity</param>
    /// <param name="conflictType">Semantic classification of conflict</param>
    /// <returns>Recommended merge strategy</returns>
    Task<ResolutionStrategy> SelectStrategyAsync(
        ConflictInfo conflict,
        SemanticConflictType conflictType);

    /// <summary>
    /// Get all applicable merge strategies for a given conflict.
    /// Allows trying multiple strategies if one fails.
    /// </summary>
    /// <param name="conflict">Conflict information</param>
    /// <param name="conflictType">Semantic classification of conflict</param>
    /// <returns>List of applicable strategies, ordered by safety/preference</returns>
    Task<List<ResolutionStrategy>> GetAvailableStrategiesAsync(
        ConflictInfo conflict,
        SemanticConflictType conflictType);
}
