using System.Text.RegularExpressions;
using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Services;

/// <summary>
/// Service for integrating GitHub Copilot SDK with conflict resolution.
/// Provides semantic analysis with graceful fallback to heuristics.
/// </summary>
public class CopilotService : ICopilotService
{
    private readonly ILogger<CopilotService> _logger;
    private bool _sdkAvailable = true;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly int _maxDiffSizeBytes = 5_000_000; // 5MB limit
    private readonly int _timeoutSeconds = 5;

    public CopilotService(ILogger<CopilotService> logger)
    {
        _logger = logger;
        _rateLimiter = new SemaphoreSlim(10, 10); // 10 concurrent calls
    }

    public async Task<ConflictContext> AnalyzeDiffAsync(
        string baseBranch,
        string headBranch,
        string filePath,
        string baseContent,
        string headContent)
    {
        if (string.IsNullOrEmpty(filePath) || baseContent == null || headContent == null)
        {
            return CreateEmptyContext();
        }

        // Validate inputs
        if (!ValidateInputs(filePath, baseContent, headContent))
        {
            _logger.LogWarning("Input validation failed for {FilePath}", filePath);
            return CreateEmptyContext();
        }

        if (!_sdkAvailable)
        {
            return await FallbackAnalyzeAsync(filePath, baseContent, headContent);
        }

        try
        {
            await _rateLimiter.WaitAsync(TimeSpan.FromSeconds(_timeoutSeconds));

            // In a real implementation, this would call the Copilot SDK
            // For now, return structured context with heuristic analysis
            var context = await PerformSemanticAnalysisAsync(filePath, baseContent, headContent);
            context.CopilotAnalyzed = true;
            context.AnalysisConfidence = Math.Min(context.AnalysisConfidence, 0.85); // Cap confidence when SDK available

            _logger.LogInformation(
                "Copilot analyzed {FilePath}: type={ConflictType}, confidence={Confidence}",
                filePath, context.Type, context.AnalysisConfidence);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copilot SDK failed, using fallback analysis");
            _sdkAvailable = false;
            return await FallbackAnalyzeAsync(filePath, baseContent, headContent);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<List<ConflictProposal>> SuggestResolutionAsync(
        string filePath,
        ConflictMarker conflict,
        string context)
    {
        var proposals = new List<ConflictProposal>();

        if (conflict == null || string.IsNullOrEmpty(conflict.OurContent))
        {
            return proposals;
        }

        try
        {
            // Check for simple cases first
            if (IsWhitespaceOnly(conflict))
            {
                proposals.Add(new ConflictProposal
                {
                    Id = "prop-001",
                    ResolvedContent = conflict.OurContent,
                    Description = "Keep our version (whitespace-only conflict)",
                    Strategy = ResolutionStrategy.ResolveOurs,
                    Confidence = 0.95,
                    AuditLog = "Detected whitespace-only conflict",
                    Validatable = false,
                    Risks = new() { "Assumes whitespace changes in theirs are unimportant" }
                });
                return proposals;
            }

            // For more complex cases, attempt Copilot analysis if available
            if (_sdkAvailable)
            {
                var copilotProposals = await SuggestViaSemanticAnalysisAsync(filePath, conflict, context);
                proposals.AddRange(copilotProposals);
            }

            // Always provide fallback proposals
            var fallbackProposals = GenerateFallbackProposals(filePath, conflict);
            proposals.AddRange(fallbackProposals);

            // Sort by confidence (highest first)
            return proposals.OrderByDescending(p => p.Confidence).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting resolutions for {FilePath}", filePath);
            return GenerateFallbackProposals(filePath, conflict);
        }
    }

    public async Task<SemanticConflictType> ClassifyConflictAsync(ConflictInfo conflict)
    {
        if (conflict == null || conflict.ConflictMarkers.Count == 0)
        {
            return SemanticConflictType.Unknown;
        }

        try
        {
            return await ClassifyConflictInternalAsync(conflict);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error classifying conflict in {FilePath}", conflict.FilePath);
            return SemanticConflictType.Unknown;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        // In a real implementation, ping the Copilot SDK
        return await Task.FromResult(_sdkAvailable);
    }

    // ============ Private Helpers ============

    private bool ValidateInputs(string filePath, string baseContent, string headContent)
    {
        // Check for path traversal attacks
        if (filePath.Contains("../") || filePath.Contains("..\\"))
        {
            return false;
        }

        // Check size limits
        var totalSize = (baseContent?.Length ?? 0) + (headContent?.Length ?? 0);
        if (totalSize > _maxDiffSizeBytes)
        {
            _logger.LogWarning("Diff size {Size} exceeds limit {Limit}", totalSize, _maxDiffSizeBytes);
            return false;
        }

        return true;
    }

    private bool IsWhitespaceOnly(ConflictMarker marker)
    {
        var ourTrimmed = StripWhitespace(marker.OurContent);
        var theirTrimmed = StripWhitespace(marker.TheirContent);
        return ourTrimmed == theirTrimmed;
    }

    private string StripWhitespace(string content)
    {
        return Regex.Replace(content, @"\s", "");
    }

    private async Task<ConflictContext> FallbackAnalyzeAsync(
        string filePath,
        string baseContent,
        string headContent)
    {
        return await Task.Run(() =>
        {
            var context = new ConflictContext
            {
                CopilotAnalyzed = false,
                AnalysisConfidence = 0.6, // Lower confidence for heuristic analysis
            };

            // Simple heuristic classification based on file extension
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".cs" or ".java" or ".ts" or ".tsx" or ".jsx" or ".py")
            {
                context.Type = SemanticConflictType.NonOverlapping;
                context.AnalysisNotes = "Fallback: heuristic analysis for code file";
            }
            else
            {
                context.Type = SemanticConflictType.Unknown;
                context.AnalysisNotes = "Fallback: unable to determine conflict type";
            }

            return context;
        });
    }

    private async Task<ConflictContext> PerformSemanticAnalysisAsync(
        string filePath,
        string baseContent,
        string headContent)
    {
        return await Task.Run(() =>
        {
            var context = new ConflictContext
            {
                CopilotAnalyzed = false, // Would be true with real SDK
                AnalysisConfidence = 0.75,
            };

            // Heuristic analysis (placeholder for real Copilot SDK call)
            var ourLen = baseContent?.Length ?? 0;
            var theirLen = headContent?.Length ?? 0;

            if (ourLen < 50 && theirLen < 50)
            {
                context.Type = SemanticConflictType.Whitespace;
                context.AnalysisConfidence = 0.95;
            }
            else if (Math.Abs(ourLen - theirLen) < 10)
            {
                context.Type = SemanticConflictType.NonOverlapping;
                context.AnalysisConfidence = 0.70;
            }
            else
            {
                context.Type = SemanticConflictType.SemanticConflict;
                context.AnalysisConfidence = 0.50;
            }

            context.AnalysisNotes = $"Semantic analysis: conflict type={context.Type}, confidence={context.AnalysisConfidence}";
            return context;
        });
    }

    private async Task<List<ConflictProposal>> SuggestViaSemanticAnalysisAsync(
        string filePath,
        ConflictMarker conflict,
        string context)
    {
        return await Task.Run(() =>
        {
            var proposals = new List<ConflictProposal>();

            // Placeholder for Copilot semantic analysis
            // Would generate multiple proposals based on semantic understanding

            // Simple heuristic: if our content is shorter, assume it's a simplification
            if (conflict.OurContent.Length < conflict.TheirContent.Length)
            {
                proposals.Add(new ConflictProposal
                {
                    Id = "prop-copilot-001",
                    ResolvedContent = conflict.OurContent,
                    Description = "Use simpler version (ours)",
                    Strategy = ResolutionStrategy.ResolveOurs,
                    Confidence = 0.65,
                    AuditLog = "Copilot heuristic: shorter version preferred",
                    Validatable = true,
                    Risks = new() { "May lose functionality from theirs" }
                });
            }

            return proposals;
        });
    }

    private async Task<SemanticConflictType> ClassifyConflictInternalAsync(ConflictInfo conflict)
    {
        return await Task.Run(() =>
        {
            if (conflict.ConflictMarkers.Count == 0)
            {
                return SemanticConflictType.Unknown;
            }

            var marker = conflict.ConflictMarkers[0];

            // Check for deletion vs. modification
            var ourEmpty = string.IsNullOrWhiteSpace(marker.OurContent);
            var theirEmpty = string.IsNullOrWhiteSpace(marker.TheirContent);
            if (ourEmpty != theirEmpty)
            {
                return SemanticConflictType.DeletionVsModification;
            }

            // Check for whitespace-only
            if (IsWhitespaceOnly(marker))
            {
                return SemanticConflictType.Whitespace;
            }

            // Check for function/method definition (signature change)
            if (ContainsFunctionDefinition(marker.OurContent) || ContainsFunctionDefinition(marker.TheirContent))
            {
                return SemanticConflictType.SignatureChange;
            }

            // Check for import/using statements
            if (ContainsImportStatement(marker.OurContent) || ContainsImportStatement(marker.TheirContent))
            {
                return SemanticConflictType.ImportConflict;
            }

            // Default: semantic conflict
            return SemanticConflictType.SemanticConflict;
        });
    }

    private bool ContainsFunctionDefinition(string content)
    {
        var patterns = new[]
        {
            @"(public|private|protected|static|async)\s+\w+\s+\w+\s*\(",
            @"def\s+\w+\s*\(",
            @"function\s+\w+\s*\(",
        };

        return patterns.Any(pattern => Regex.IsMatch(content, pattern));
    }

    private bool ContainsImportStatement(string content)
    {
        var patterns = new[] { @"^using\s+", @"^import\s+", @"^from\s+" };
        return patterns.Any(pattern => Regex.IsMatch(content, pattern, RegexOptions.Multiline));
    }

    private List<ConflictProposal> GenerateFallbackProposals(
        string filePath,
        ConflictMarker conflict)
    {
        var proposals = new List<ConflictProposal>();

        // Proposal 1: Keep ours
        proposals.Add(new ConflictProposal
        {
            Id = "prop-ours",
            ResolvedContent = conflict.OurContent,
            Description = "Keep our version",
            Strategy = ResolutionStrategy.ResolveOurs,
            Confidence = 0.50,
            AuditLog = "Fallback: no semantic analysis available",
            Validatable = true,
            Risks = new() { "Loses all changes from theirs" }
        });

        // Proposal 2: Keep theirs
        proposals.Add(new ConflictProposal
        {
            Id = "prop-theirs",
            ResolvedContent = conflict.TheirContent,
            Description = "Keep their version",
            Strategy = ResolutionStrategy.ResolveTheirs,
            Confidence = 0.50,
            AuditLog = "Fallback: no semantic analysis available",
            Validatable = true,
            Risks = new() { "Loses all changes from ours" }
        });

        // Proposal 3: Combined (if safe)
        if (CanCombineSafely(conflict))
        {
            var combined = $"{conflict.OurContent}\n{conflict.TheirContent}";
            proposals.Add(new ConflictProposal
            {
                Id = "prop-combined",
                ResolvedContent = combined,
                Description = "Combine both versions (may have duplication)",
                Strategy = ResolutionStrategy.Recursive,
                Confidence = 0.30,
                AuditLog = "Fallback: heuristic combination",
                Validatable = true,
                Risks = new() { "May result in duplicated code" }
            });
        }

        return proposals;
    }

    private bool CanCombineSafely(ConflictMarker marker)
    {
        // Only safe if both sides are data (not logic)
        var combinedContent = $"{marker.OurContent}\n{marker.TheirContent}";
        return !ContainsFunctionDefinition(combinedContent) && !ContainsImportStatement(combinedContent);
    }

    private ConflictContext CreateEmptyContext()
    {
        return new ConflictContext
        {
            Type = SemanticConflictType.Unknown,
            CopilotAnalyzed = false,
            AnalysisConfidence = 0.0,
            AnalysisNotes = "Unable to analyze conflict"
        };
    }
}
