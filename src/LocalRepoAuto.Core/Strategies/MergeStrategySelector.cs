using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Strategies;

/// <summary>
/// Selector for optimal merge strategy based on conflict type and complexity.
/// </summary>
public class MergeStrategySelector : IMergeStrategySelector
{
    private readonly ILogger<MergeStrategySelector> _logger;

    public MergeStrategySelector(ILogger<MergeStrategySelector> logger)
    {
        _logger = logger;
    }

    public async Task<ResolutionStrategy> SelectStrategyAsync(
        ConflictInfo conflict,
        SemanticConflictType conflictType)
    {
        return await Task.Run(() =>
        {
            var strategy = SelectStrategyInternal(conflict, conflictType);
            _logger.LogInformation(
                "Selected strategy {Strategy} for {FilePath} (type={ConflictType}, complexity={Complexity})",
                strategy, conflict.FilePath, conflictType, conflict.Complexity);
            return strategy;
        });
    }

    public async Task<List<ResolutionStrategy>> GetAvailableStrategiesAsync(
        ConflictInfo conflict,
        SemanticConflictType conflictType)
    {
        return await Task.Run(() =>
        {
            var strategies = GetAvailableStrategiesInternal(conflict, conflictType);
            _logger.LogDebug(
                "Available strategies for {FilePath}: {Strategies}",
                conflict.FilePath, string.Join(", ", strategies));
            return strategies;
        });
    }

    // ============ Private Helpers ============

    private ResolutionStrategy SelectStrategyInternal(
        ConflictInfo conflict,
        SemanticConflictType conflictType)
    {
        // Priority 1: Whitespace-only conflicts
        if (conflictType == SemanticConflictType.Whitespace)
        {
            _logger.LogDebug("Whitespace-only conflict: selecting ResolveOurs");
            return ResolutionStrategy.ResolveOurs;
        }

        // Priority 2: Deletion vs. Modification (require human review)
        if (conflictType == SemanticConflictType.DeletionVsModification)
        {
            _logger.LogDebug("Deletion vs. Modification conflict: requires human review");
            return ResolutionStrategy.RequiresHumanReview;
        }

        // Priority 3: Signature changes (require human review)
        if (conflictType == SemanticConflictType.SignatureChange)
        {
            _logger.LogDebug("Signature change detected: requires human review");
            return ResolutionStrategy.RequiresHumanReview;
        }

        // Priority 4: Complex semantic conflicts
        if (conflictType == SemanticConflictType.SemanticConflict)
        {
            if (conflict.Complexity == Interfaces.ConflictComplexity.Complex)
            {
                _logger.LogDebug("Complex semantic conflict: selecting ORT");
                return ResolutionStrategy.ORT;
            }

            _logger.LogDebug("Medium semantic conflict: selecting Recursive");
            return ResolutionStrategy.Recursive;
        }

        // Priority 5: Non-overlapping changes (safe to merge)
        if (conflictType == SemanticConflictType.NonOverlapping)
        {
            _logger.LogDebug("Non-overlapping conflict: selecting Recursive");
            return ResolutionStrategy.Recursive;
        }

        // Priority 6: Import conflicts
        if (conflictType == SemanticConflictType.ImportConflict)
        {
            _logger.LogDebug("Import conflict: selecting ORT");
            return ResolutionStrategy.ORT;
        }

        // Default: Safe fallback
        _logger.LogDebug("Default conflict type: selecting Recursive");
        return ResolutionStrategy.Recursive;
    }

    private List<ResolutionStrategy> GetAvailableStrategiesInternal(
        ConflictInfo conflict,
        SemanticConflictType conflictType)
    {
        var strategies = new List<ResolutionStrategy>();

        // Always available: resolve-ours and resolve-theirs (safest but lose changes)
        strategies.Add(ResolutionStrategy.ResolveOurs);
        strategies.Add(ResolutionStrategy.ResolveTheirs);

        // Available for non-critical conflicts
        if (conflictType != SemanticConflictType.DeletionVsModification &&
            conflictType != SemanticConflictType.SignatureChange)
        {
            strategies.Add(ResolutionStrategy.Recursive);
        }

        // Available for non-simple conflicts
        if (conflict.Complexity != Interfaces.ConflictComplexity.Simple)
        {
            strategies.Add(ResolutionStrategy.ORT);
        }

        // Always available as last resort
        strategies.Add(ResolutionStrategy.RequiresHumanReview);

        return strategies;
    }
}
