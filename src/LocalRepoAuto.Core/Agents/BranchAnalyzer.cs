using LocalRepoAuto.Core.Exceptions;
using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Agents;

/// <summary>
/// Agent for analyzing branches and their staleness.
/// Orchestrates GitOperations and StalenessHeuristics to provide comprehensive branch inventory.
/// </summary>
public class BranchAnalyzer : IBranchAnalyzer
{
    private readonly IGitOperations _gitOps;
    private readonly IStalenessHeuristics _staleness;
    private readonly ILogger<BranchAnalyzer> _logger;

    public BranchAnalyzer(
        IGitOperations gitOps,
        IStalenessHeuristics staleness,
        ILogger<BranchAnalyzer> logger)
    {
        _gitOps = gitOps;
        _staleness = staleness;
        _logger = logger;
    }

    public async Task<List<BranchInfo>> ListBranchesAsync()
    {
        _logger.LogInformation("Listing all branches");

        var branchNames = await _gitOps.GetBranchesAsync();
        var branches = new List<BranchInfo>();

        foreach (var branchName in branchNames)
        {
            try
            {
                var branch = await GetBranchMetadataAsync(branchName);
                branches.Add(branch);
            }
            catch (InvalidRefException)
            {
                _logger.LogWarning("Failed to get metadata for branch {BranchName} - skipping", branchName);
            }
        }

        // Sort by last commit date (newest first)
        branches = branches.OrderByDescending(b => b.LastCommitDate).ToList();

        _logger.LogInformation("Listed {BranchCount} branches successfully", branches.Count);
        return branches;
    }

    public async Task<BranchInfo> GetBranchMetadataAsync(string branchName)
    {
        _logger.LogDebug("Getting metadata for branch {BranchName}", branchName);

        var metadata = await _gitOps.GetCommitMetadataAsync(branchName);
        var now = DateTime.UtcNow;
        var daysSinceCommit = (int)(now - metadata.CommitDate).TotalDays;

        var branchInfo = new BranchInfo
        {
            Name = branchName,
            LastCommitHash = metadata.ShortHash,
            LastCommitHashFull = metadata.Hash,
            LastCommitDate = metadata.CommitDate,
            LastAuthor = metadata.AuthorName,
            LastAuthorEmail = metadata.AuthorEmail,
            LastCommitMessage = metadata.Message,
            IsLocalOnly = !branchName.Contains("/"), // Simplification; Phase 3 refines this
            IsProtected = await _staleness.IsProtectedBranchAsync(branchName),
            DaysStale = daysSinceCommit,
            IsCurrentBranch = branchName == (await _gitOps.GetCurrentBranchAsync()),
            IsDetachedHead = branchName == "(HEAD detached)"
        };

        // Calculate staleness score
        var stalenessScore = await _staleness.CalculateStalenessAsync(branchInfo);
        branchInfo.StalenessScore = stalenessScore.Score;
        branchInfo.StalenessReason = stalenessScore.Reason;
        branchInfo.StalenessConfidence = stalenessScore.ConfidenceLevel;

        return branchInfo;
    }

    public async Task<List<BranchInfo>> DetectStaleBranchesAsync(int? thresholdDays = null)
    {
        int threshold = thresholdDays ?? _staleness.GetThresholdDays();
        _logger.LogInformation("Detecting stale branches (threshold: {ThresholdDays} days)", threshold);

        var allBranches = await ListBranchesAsync();

        var staleBranches = allBranches
            .Where(b => 
                !b.IsProtected &&
                b.DaysStale >= threshold &&
                b.StalenessScore > 0 &&
                !b.IsCurrentBranch)
            .OrderByDescending(b => b.StalenessScore)
            .ToList();

        _logger.LogInformation("Found {StaleCount} stale branches out of {TotalCount}",
            staleBranches.Count, allBranches.Count);

        return staleBranches;
    }

    public async Task<StalenessScore> AnalyzeStalenessAsync(string branchName)
    {
        _logger.LogInformation("Analyzing staleness for branch {BranchName}", branchName);

        var branchInfo = await GetBranchMetadataAsync(branchName);
        return await _staleness.CalculateStalenessAsync(branchInfo);
    }
}
