using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalRepoAuto.Core.Analysis;

/// <summary>
/// Staleness heuristics engine for calculating branch staleness scores.
/// Combines time-based, name-based, and author-activity signals.
/// </summary>
public class StalenessHeuristics : IStalenessHeuristics
{
    private readonly ILogger<StalenessHeuristics> _logger;
    private readonly StalenessConfig _config;

    public StalenessHeuristics(ILogger<StalenessHeuristics> logger, IOptions<StalenessConfig> options)
    {
        _logger = logger;
        _config = options.Value;
    }

    public async Task<StalenessScore> CalculateStalenessAsync(BranchInfo branchInfo)
    {
        // Protected branches never stale
        if (await IsProtectedBranchAsync(branchInfo.Name))
        {
            return new StalenessScore
            {
                Score = 0,
                Reason = "Protected branch (never marked stale)",
                ConfidenceLevel = 1.0,
                IsProtected = true,
                Components = new Dictionary<string, int> { { "protected", 0 } },
                LastCommitDate = branchInfo.LastCommitDate,
                DaysSinceCommit = branchInfo.DaysStale,
                ThresholdDays = _config.ThresholdDays
            };
        }

        var components = new Dictionary<string, int>();
        var reasons = new List<string>();

        // 1. Time-based score
        int timeScore = CalculateTimeScore(branchInfo.DaysStale, out var timeReason);
        components["time"] = timeScore;
        if (timeScore > 0)
            reasons.Add(timeReason);

        // 2. Name pattern scoring
        int nameScore = CalculateNamePatternScore(branchInfo.Name, out var nameReason);
        components["name_pattern"] = nameScore;
        if (nameScore > 0)
            reasons.Add(nameReason);

        // 3. Author activity scoring
        int authorScore = CalculateAuthorActivityScore(branchInfo.LastAuthor, out var authorReason);
        components["author_activity"] = authorScore;
        if (authorScore > 0)
            reasons.Add(authorReason);

        // 4. Calculate confidence
        double confidence = CalculateConfidence(branchInfo.LastAuthor, authorScore);

        // Final score
        int finalScore = Math.Min(timeScore + nameScore + authorScore, 100);

        return new StalenessScore
        {
            Score = finalScore,
            Reason = string.Join(" + ", reasons),
            ConfidenceLevel = confidence,
            Components = components,
            LastCommitDate = branchInfo.LastCommitDate,
            DaysSinceCommit = branchInfo.DaysStale,
            ThresholdDays = _config.ThresholdDays,
            IsProtected = false
        };
    }

    public async Task<bool> IsProtectedBranchAsync(string branchName)
    {
        // Check configured protected branches
        if (_config.ProtectedBranches != null)
        {
            foreach (var pattern in _config.ProtectedBranches)
            {
                if (pattern.EndsWith("*"))
                {
                    var prefix = pattern.TrimEnd('*');
                    if (branchName.StartsWith(prefix))
                        return true;
                }
                else if (branchName == pattern)
                {
                    return true;
                }
            }
        }

        // Default protected branches
        var defaultProtected = new[] { "main", "master", "develop", "staging", "production" };
        if (defaultProtected.Contains(branchName))
            return true;

        // Release branches
        if (branchName.StartsWith("release/"))
            return true;

        return await Task.FromResult(false);
    }

    public int GetThresholdDays()
    {
        return _config.ThresholdDays;
    }

    public List<string> GetProtectedBranches()
    {
        var protected_branches = _config.ProtectedBranches?.ToList() ?? new List<string>();
        protected_branches.AddRange(new[] { "main", "master", "develop", "staging", "production" });
        return protected_branches.Distinct().ToList();
    }

    // ============ Private Helpers ============

    private int CalculateTimeScore(int daysSinceCommit, out string reason)
    {
        reason = "";
        int score = 0;

        if (daysSinceCommit <= _config.ThresholdDays)
        {
            score = 0;
            reason = $"Recently updated ({daysSinceCommit} days ago)";
        }
        else if (daysSinceCommit <= _config.ThresholdDays * 2)
        {
            score = (int)((double)(daysSinceCommit - _config.ThresholdDays) / _config.ThresholdDays * 50);
            reason = $"{daysSinceCommit} days old (+{score} points)";
        }
        else
        {
            score = Math.Min(100, (int)((double)daysSinceCommit / _config.ThresholdDays * 50));
            reason = $"{daysSinceCommit} days old (+{score} points)";
        }

        return score;
    }

    private int CalculateNamePatternScore(string branchName, out string reason)
    {
        reason = "";
        int score = 0;

        // Check configured pattern penalties
        if (_config.PatternPenalties != null)
        {
            foreach (var (pattern, penalty) in _config.PatternPenalties)
            {
                if (pattern.EndsWith("*"))
                {
                    var prefix = pattern.TrimEnd('*');
                    if (branchName.StartsWith(prefix))
                    {
                        score = penalty;
                        reason = $"Branch name pattern '{pattern}' adds {penalty} points";
                        break;
                    }
                }
                else if (branchName.StartsWith(pattern))
                {
                    score = penalty;
                    reason = $"Branch name pattern '{pattern}' adds {penalty} points";
                    break;
                }
            }
        }

        return score;
    }

    private int CalculateAuthorActivityScore(string authorName, out string reason)
    {
        reason = "";

        // In Phase 3, we can add git log analysis to check author activity
        // For now, just log it
        if (string.IsNullOrWhiteSpace(authorName) || authorName == "Unknown")
        {
            return 5; // Small penalty for unknown author
        }

        return 0;
    }

    private double CalculateConfidence(string authorName, int authorActivityScore)
    {
        // Lower confidence if we have low author activity signal
        if (string.IsNullOrWhiteSpace(authorName) || authorName == "Unknown")
        {
            return 0.7; // 70% confidence if author unknown
        }

        if (authorActivityScore > 0)
        {
            return 0.8; // 80% confidence if author seems inactive
        }

        return 1.0; // 100% confidence if author is known and recent
    }
}

/// <summary>
/// Configuration for staleness detection.
/// </summary>
public class StalenessConfig
{
    public int ThresholdDays { get; set; } = 30;
    public List<string> ProtectedBranches { get; set; } = new();
    public Dictionary<string, int> PatternPenalties { get; set; } = new();
    public int AuthorInactivityDays { get; set; } = 90;
}
