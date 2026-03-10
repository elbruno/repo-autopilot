using System.Text.RegularExpressions;
using LocalRepoAuto.Core.Exceptions;
using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Analysis;

/// <summary>
/// Detects and analyzes merge conflicts between branches.
/// </summary>
public class ConflictDetector : IConflictDetector
{
    private readonly IGitOperations _gitOps;
    private readonly ILogger<ConflictDetector> _logger;

    public ConflictDetector(IGitOperations gitOps, ILogger<ConflictDetector> logger)
    {
        _gitOps = gitOps;
        _logger = logger;
    }

    public async Task<List<ConflictInfo>> DetectConflictsAsync(string baseBranch, string headBranch)
    {
        _logger.LogInformation("Detecting conflicts between {BaseBranch} and {HeadBranch}", 
            baseBranch, headBranch);

        var mergeResult = await PerformDryRunMergeAsync(baseBranch, headBranch);

        if (mergeResult.Success)
        {
            return new List<ConflictInfo>();
        }

        return mergeResult.Conflicts;
    }

    public async Task<ConflictComplexity> AnalyzeConflictComplexityAsync(string filePath, string conflictContent)
    {
        return await Task.Run(() =>
        {
            var complexity = AnalyzeComplexity(filePath, conflictContent);
            _logger.LogDebug("Conflict in {FilePath} analyzed as {Complexity}", filePath, complexity);
            return complexity;
        });
    }

    public async Task<MergeResult> PerformDryRunMergeAsync(string baseBranch, string headBranch)
    {
        try
        {
            _logger.LogInformation("Performing dry-run merge of {HeadBranch} into {BaseBranch}", 
                headBranch, baseBranch);

            return await _gitOps.MergeBranchesAsync(baseBranch, headBranch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dry-run merge");
            return new MergeResult
            {
                Success = false,
                Error = ex.Message,
                IsDryRun = true,
                Conflicts = new List<ConflictInfo>()
            };
        }
    }

    // ============ Private Helpers ============

    private ConflictComplexity AnalyzeComplexity(string filePath, string conflictContent)
    {
        int score = CalculateComplexityScore(filePath, conflictContent);

        if (score <= 30)
            return ConflictComplexity.Simple;
        else if (score <= 70)
            return ConflictComplexity.Medium;
        else
            return ConflictComplexity.Complex;
    }

    private int CalculateComplexityScore(string filePath, string conflictContent)
    {
        int score = 0;

        // 1. Check if whitespace-only
        if (IsWhitespaceOnly(conflictContent))
        {
            return 5; // Simple
        }

        // 2. Line count analysis
        var markers = ExtractConflictMarkers(conflictContent);
        if (markers.Count == 0)
        {
            return 0;
        }

        foreach (var marker in markers)
        {
            int ourLines = marker.OurLineCount;
            int theirLines = marker.TheirLineCount;
            int maxLines = Math.Max(ourLines, theirLines);

            // Large conflicts are more complex
            if (maxLines > 20)
                score += 15;
            else if (maxLines > 10)
                score += 10;

            // Check for semantic indicators
            var combinedContent = marker.OurContent + marker.TheirContent;

            // Variable/function name changes (potential semantic conflict)
            if (ContainsFunctionDefinition(combinedContent))
                score += 30;

            // Multiple changes in same region
            if (ourLines > 1 && theirLines > 1)
                score += 20;
        }

        // File extension penalties
        if (filePath.EndsWith(".cs") || filePath.EndsWith(".java") || filePath.EndsWith(".ts"))
        {
            // Code files often have semantic conflicts
            score = Math.Max(score, 25);
        }

        // Check for deletion vs modification (high complexity)
        if (HasDeletionVsModificationConflict(conflictContent))
        {
            score = Math.Min(score + 40, 100);
        }

        return Math.Min(score, 100);
    }

    private bool IsWhitespaceOnly(string conflictContent)
    {
        var markers = ExtractConflictMarkers(conflictContent);
        
        foreach (var marker in markers)
        {
            var ourTrimmed = marker.OurContent.Replace(" ", "").Replace("\t", "").Replace("\n", "");
            var theirTrimmed = marker.TheirContent.Replace(" ", "").Replace("\t", "").Replace("\n", "");

            if (ourTrimmed != theirTrimmed)
            {
                // Non-whitespace differences
                return false;
            }
        }

        return true;
    }

    private List<ConflictMarker> ExtractConflictMarkers(string content)
    {
        var markers = new List<ConflictMarker>();
        
        // Pattern for conflict markers
        var pattern = @"<<<<<<< (.+?)(.+?)=======(.+?)>>>>>>> (.+?)(?:\n|$)";
        var regex = new Regex(pattern, RegexOptions.Singleline);

        int lineNum = 1;
        foreach (Match match in regex.Matches(content))
        {
            var ourLabel = match.Groups[1].Value.Trim();
            var ourContent = match.Groups[2].Value;
            var theirContent = match.Groups[3].Value;
            var theirLabel = match.Groups[4].Value.Trim();

            int ourLines = ourContent.Split('\n').Length;
            int theirLines = theirContent.Split('\n').Length;

            markers.Add(new ConflictMarker
            {
                StartLine = lineNum,
                EndLine = lineNum + ourLines + theirLines + 2,
                OurContent = ourContent.Trim(),
                TheirContent = theirContent.Trim(),
                OurLabel = ourLabel,
                TheirLabel = theirLabel
            });

            lineNum += ourLines + theirLines + 4;
        }

        return markers;
    }

    private bool ContainsFunctionDefinition(string content)
    {
        // Check for C# / Java / TypeScript function definitions
        var patterns = new[]
        {
            @"(public|private|protected|static|async)\s+\w+\s+\w+\s*\(",
            @"def\s+\w+\s*\(", // Python
            @"function\s+\w+\s*\(", // JavaScript
        };

        foreach (var pattern in patterns)
        {
            if (Regex.IsMatch(content, pattern))
                return true;
        }

        return false;
    }

    private bool HasDeletionVsModificationConflict(string conflictContent)
    {
        var markers = ExtractConflictMarkers(conflictContent);

        foreach (var marker in markers)
        {
            bool ourEmpty = string.IsNullOrWhiteSpace(marker.OurContent);
            bool theirEmpty = string.IsNullOrWhiteSpace(marker.TheirContent);

            // One side deleted, other modified = semantic conflict
            if (ourEmpty != theirEmpty)
            {
                return true;
            }
        }

        return false;
    }
}
