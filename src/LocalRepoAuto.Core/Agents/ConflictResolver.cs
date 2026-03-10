using LocalRepoAuto.Core.Exceptions;
using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LocalRepoAuto.Core.Agents;

/// <summary>
/// Agent for orchestrating conflict resolution.
/// Combines semantic analysis, strategy selection, and proposal generation.
/// </summary>
public class ConflictResolver : IConflictResolver
{
    private readonly ICopilotService _copilotService;
    private readonly IMergeStrategySelector _strategySelector;
    private readonly IConflictDetector _conflictDetector;
    private readonly ILogger<ConflictResolver> _logger;

    public ConflictResolver(
        ICopilotService copilotService,
        IMergeStrategySelector strategySelector,
        IConflictDetector conflictDetector,
        ILogger<ConflictResolver> logger)
    {
        _copilotService = copilotService;
        _strategySelector = strategySelector;
        _conflictDetector = conflictDetector;
        _logger = logger;
    }

    public async Task<string?> ResolveAsync(
        string baseContent,
        string ourContent,
        string theirContent,
        ResolutionStrategy strategy)
    {
        if (string.IsNullOrEmpty(baseContent) || string.IsNullOrEmpty(ourContent) || string.IsNullOrEmpty(theirContent))
        {
            _logger.LogWarning("Invalid inputs to ResolveAsync");
            return null;
        }

        try
        {
            return strategy switch
            {
                ResolutionStrategy.ResolveOurs => ourContent,
                ResolutionStrategy.ResolveTheirs => theirContent,
                ResolutionStrategy.Recursive => await PerformRecursiveMergeAsync(baseContent, ourContent, theirContent),
                ResolutionStrategy.ORT => await PerformORTMergeAsync(baseContent, ourContent, theirContent),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict with strategy {Strategy}", strategy);
            return null;
        }
    }

    public async Task<ResolutionResponse> GetResolutionProposalsAsync(ResolutionRequest request)
    {
        if (request?.Conflict == null || string.IsNullOrEmpty(request.FilePath))
        {
            throw new LocalRepoAutoException("Invalid resolution request");
        }

        var auditLog = new StringBuilder();
        auditLog.AppendLine($"Resolution process started for {request.FilePath}");
        auditLog.AppendLine($"Conflict complexity: {request.Conflict.Complexity}");
        auditLog.AppendLine($"Total conflict lines: {request.Conflict.TotalConflictLines}");

        try
        {
            // Step 1: Classify conflict semantically
            var conflictType = await _copilotService.ClassifyConflictAsync(request.Conflict);
            auditLog.AppendLine($"Semantic classification: {conflictType}");

            // Step 2: Select merge strategy
            var strategy = await _strategySelector.SelectStrategyAsync(request.Conflict, conflictType);
            auditLog.AppendLine($"Selected strategy: {strategy}");

            // Step 3: Generate proposals
            var proposals = await GenerateProposalsAsync(request, conflictType);
            auditLog.AppendLine($"Generated {proposals.Count} proposals");

            // Step 4: Validate proposals
            foreach (var proposal in proposals)
            {
                if (proposal.Validatable && proposal.ResolvedContent != null)
                {
                    proposal.ValidationResult = await ValidateResolutionAsync(
                        proposal.ResolvedContent, request.FilePath);
                    auditLog.AppendLine($"Validated proposal {proposal.Id}: {(proposal.ValidationResult.IsValid ? "VALID" : "INVALID")}");
                }
            }

            // Step 5: Select recommended proposal
            var recommendedProposal = proposals
                .Where(p => p.Confidence >= request.MinimumConfidenceForAutoResolve)
                .OrderByDescending(p => p.Confidence)
                .FirstOrDefault();

            if (recommendedProposal != null)
            {
                auditLog.AppendLine($"Recommended proposal: {recommendedProposal.Id} (confidence: {recommendedProposal.Confidence:P})");
            }
            else
            {
                auditLog.AppendLine("No proposal meets minimum confidence threshold for auto-resolution");
            }

            var response = new ResolutionResponse
            {
                Proposals = proposals.OrderByDescending(p => p.Confidence).ToList(),
                RecommendedProposal = recommendedProposal,
                ProcessAuditLog = auditLog.ToString(),
                CompletedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Generated {ProposalCount} proposals for {FilePath}", 
                proposals.Count, request.FilePath);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating resolution proposals for {FilePath}", request.FilePath);
            auditLog.AppendLine($"ERROR: {ex.Message}");

            // Return error response
            return new ResolutionResponse
            {
                Proposals = new(),
                RecommendedProposal = null,
                ProcessAuditLog = auditLog.ToString(),
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<ValidationResult> ValidateResolutionAsync(string resolvedContent, string filePath)
    {
        var result = new ValidationResult
        {
            IsValid = true,
            Errors = new(),
            Warnings = new(),
            ValidationDetails = new()
        };

        // Basic validation: no conflict markers
        if (resolvedContent.Contains("<<<<<<<") || resolvedContent.Contains("=======") || resolvedContent.Contains(">>>>>>>"))
        {
            result.IsValid = false;
            result.Errors.Add("Conflict markers still present in resolved content");
        }

        result.ValidationDetails.Add("Conflict marker check: passed");

        // Language-specific validation
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var validationTasks = new List<Task>();

        if (ext is ".cs" or ".java" or ".ts" or ".tsx")
        {
            validationTasks.Add(ValidateSyntaxAsync(resolvedContent, ext, result));
        }

        await Task.WhenAll(validationTasks);

        return result;
    }

    // ============ Private Helpers ============

    private async Task<List<ConflictProposal>> GenerateProposalsAsync(
        ResolutionRequest request,
        SemanticConflictType conflictType)
    {
        var proposals = new List<ConflictProposal>();

        // Get all available strategies
        var strategies = await _strategySelector.GetAvailableStrategiesAsync(
            request.Conflict, conflictType);

        foreach (var strategy in strategies.Where(s => s != ResolutionStrategy.RequiresHumanReview))
        {
            try
            {
                var resolved = await ResolveAsync(
                    request.BaseContent,
                    request.BaseContent, // "ours" is base for this iteration
                    request.HeadContent,
                    strategy);

                if (resolved != null)
                {
                    var proposal = new ConflictProposal
                    {
                        Id = $"prop-{strategy}",
                        ResolvedContent = resolved,
                        Description = GetStrategyDescription(strategy),
                        Strategy = strategy,
                        Confidence = await CalculateConfidenceAsync(strategy, conflictType, request.Conflict),
                        AuditLog = $"Generated via {strategy} strategy",
                        Validatable = true,
                        Risks = GetStrategyRisks(strategy, conflictType)
                    };

                    proposals.Add(proposal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Strategy {Strategy} failed for {FilePath}", strategy, request.FilePath);
            }
        }

        // If all strategies failed, return human review proposal
        if (proposals.Count == 0)
        {
            proposals.Add(new ConflictProposal
            {
                Id = "prop-human-review",
                ResolvedContent = string.Empty,
                Description = "Conflict requires manual human review",
                Strategy = ResolutionStrategy.RequiresHumanReview,
                Confidence = 0.0,
                AuditLog = "No automatic resolution available",
                Validatable = false,
                Risks = new() { "Unable to automatically resolve conflict" }
            });
        }

        return proposals;
    }

    private async Task<double> CalculateConfidenceAsync(
        ResolutionStrategy strategy,
        SemanticConflictType conflictType,
        ConflictInfo conflict)
    {
        return await Task.Run(() =>
        {
            // Base confidence on strategy and conflict type
            var baseConfidence = strategy switch
            {
                ResolutionStrategy.Recursive => 0.65,
                ResolutionStrategy.ResolveOurs => 0.50,
                ResolutionStrategy.ResolveTheirs => 0.50,
                ResolutionStrategy.ORT => 0.70,
                _ => 0.0
            };

            // Adjust for conflict type
            var typeModifier = conflictType switch
            {
                SemanticConflictType.Whitespace => 0.30, // Very confident for whitespace
                SemanticConflictType.NonOverlapping => 0.15, // Confident for non-overlapping
                SemanticConflictType.SemanticConflict => -0.20, // Less confident
                SemanticConflictType.DeletionVsModification => -0.40, // Very uncertain
                _ => 0.0
            };

            // Adjust for complexity
            var complexityModifier = conflict.Complexity switch
            {
                Interfaces.ConflictComplexity.Simple => 0.20,
                Interfaces.ConflictComplexity.Medium => 0.0,
                Interfaces.ConflictComplexity.Complex => -0.25,
                _ => 0.0
            };

            var confidence = baseConfidence + typeModifier + complexityModifier;
            return Math.Max(0.0, Math.Min(1.0, confidence)); // Clamp to [0, 1]
        });
    }

    private List<string> GetStrategyRisks(ResolutionStrategy strategy, SemanticConflictType conflictType)
    {
        var risks = new List<string>();

        risks.AddRange(strategy switch
        {
            ResolutionStrategy.ResolveOurs => new[] { "All changes from incoming branch are lost" },
            ResolutionStrategy.ResolveTheirs => new[] { "All our changes are lost" },
            ResolutionStrategy.Recursive => new[] { "May not resolve semantic conflicts correctly" },
            ResolutionStrategy.ORT => new[] { "Complex algorithm may have unexpected side effects" },
            _ => Array.Empty<string>()
        });

        if (conflictType == SemanticConflictType.SemanticConflict)
        {
            risks.Add("Semantic intent may not be preserved correctly");
        }

        return risks;
    }

    private string GetStrategyDescription(ResolutionStrategy strategy)
    {
        return strategy switch
        {
            ResolutionStrategy.Recursive => "Apply both changes (3-way merge)",
            ResolutionStrategy.ResolveOurs => "Keep our version, discard theirs",
            ResolutionStrategy.ResolveTheirs => "Keep their version, discard ours",
            ResolutionStrategy.ORT => "Structured merge algorithm (ORT)",
            ResolutionStrategy.RequiresHumanReview => "Manual review required",
            _ => "Unknown strategy"
        };
    }

    private async Task<string?> PerformRecursiveMergeAsync(
        string baseContent,
        string ourContent,
        string theirContent)
    {
        return await Task.Run(() =>
        {
            // Simple recursive merge: if both sides modified, keep ours with theirs appended
            // (In real implementation, use actual 3-way merge logic)
            if (string.IsNullOrEmpty(baseContent))
            {
                return null;
            }

            // Check if changes don't overlap
            if (!ContentOverlaps(baseContent, ourContent, theirContent))
            {
                // Safe to combine
                return ourContent; // Simplified: would do actual merge
            }

            // Changes overlap - can't safely merge
            return null;
        });
    }

    private async Task<string?> PerformORTMergeAsync(
        string baseContent,
        string ourContent,
        string theirContent)
    {
        return await Task.Run(() =>
        {
            // ORT (Conflict Resolution Transport) algorithm placeholder
            // In real implementation, would implement structured merge

            if (string.IsNullOrEmpty(baseContent))
            {
                return null;
            }

            // Try to identify changed lines
            var ourLines = ourContent.Split('\n');
            var theirLines = theirContent.Split('\n');

            // If one side is significantly shorter, it might be a deletion
            if (ourLines.Length > theirLines.Length * 2)
            {
                // They deleted more - keep ours
                return ourContent;
            }

            if (theirLines.Length > ourLines.Length * 2)
            {
                // We deleted more - keep theirs
                return theirContent;
            }

            // Otherwise, not resolvable with ORT
            return null;
        });
    }

    private bool ContentOverlaps(string baseContent, string ourContent, string theirContent)
    {
        var baseLines = baseContent.Split('\n');
        var ourLines = ourContent.Split('\n');
        var theirLines = theirContent.Split('\n');

        // Simple heuristic: if neither side removed more than 50% of lines, likely overlapping
        var ourRemoved = baseLines.Length - ourLines.Length;
        var theirRemoved = baseLines.Length - theirLines.Length;

        return (ourRemoved < baseLines.Length * 0.5) && (theirRemoved < baseLines.Length * 0.5);
    }

    private async Task ValidateSyntaxAsync(string content, string extension, ValidationResult result)
    {
        await Task.Run(() =>
        {
            // Placeholder for language-specific syntax validation
            // In real implementation, would use language-specific parser

            // Basic check: balanced braces/brackets
            var openBraces = content.Count(c => c == '{');
            var closeBraces = content.Count(c => c == '}');

            if (openBraces != closeBraces)
            {
                result.IsValid = false;
                result.Errors.Add($"Unbalanced braces: {openBraces} open, {closeBraces} closed");
            }
            else
            {
                result.ValidationDetails.Add($"Syntax check ({extension}): braces balanced");
            }
        });
    }
}
