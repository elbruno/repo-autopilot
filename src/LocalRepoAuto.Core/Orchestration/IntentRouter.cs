using System.Text.RegularExpressions;
using LocalRepoAuto.Core.Models;
using LocalRepoAuto.Core.Safety;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Orchestration;

/// <summary>
/// Implementation of intent router.
/// Parses natural language intent, extracts parameters, validates, and routes to agents.
/// </summary>
public class IntentRouter : IIntentRouter
{
    private readonly IPreFlightChecker _safetyGuards;
    private readonly ILogger<IntentRouter> _logger;

    public IntentRouter(
        IPreFlightChecker safetyGuards,
        ILogger<IntentRouter> logger)
    {
        _safetyGuards = safetyGuards;
        _logger = logger;
    }

    /// <summary>
    /// Parse developer intent from natural language.
    /// </summary>
    public async Task<Intent> ParseIntentAsync(string userInput)
    {
        _logger.LogInformation("Parsing intent from user input: {Input}", userInput);

        var intent = new Intent { RawInput = userInput };

        if (string.IsNullOrWhiteSpace(userInput))
        {
            _logger.LogWarning("Empty intent provided");
            intent.ConfidenceLevel = 0.0;
            intent.Summary = "Invalid: empty intent";
            return intent;
        }

        userInput = userInput.Trim();
        intent.RawInput = userInput;

        // Determine intent type
        intent.Type = DetermineIntentType(userInput, out var confidence);
        intent.ConfidenceLevel = confidence;

        // Extract parameters based on intent type
        ExtractParameters(userInput, intent);

        // Generate summary
        intent.Summary = GenerateSummary(intent);

        _logger.LogInformation(
            "Intent parsed: Type={IntentType}, Confidence={Confidence}, Parameters={ParamCount}",
            intent.Type, intent.ConfidenceLevel, intent.Parameters.Count);

        return await Task.FromResult(intent);
    }

    /// <summary>
    /// Route intent to appropriate agents.
    /// </summary>
    public async Task<WorkflowRoute> RouteToAgentAsync(Intent intent)
    {
        _logger.LogInformation("Routing intent type {IntentType}", intent.Type);

        var route = new WorkflowRoute
        {
            AgentSequence = new(),
            AgentInputs = new(),
            RequiredCapabilities = new()
        };

        switch (intent.Type)
        {
            case IntentType.DeleteStaleBranches:
                route.AgentSequence.AddRange(new List<string> { "SafetyGuards", "BranchAnalyzer", "WorkflowOrchestrator" });
                route.WorkflowType = "cleanup";
                route.Description = "Delete stale branches";
                route.RequiredCapabilities.AddRange(new List<string> { "write_permissions", "no_merge_in_progress" });
                route.EstimatedDurationSeconds = 30;
                break;

            case IntentType.ResolveConflicts:
                route.AgentSequence.AddRange(new List<string> { "SafetyGuards", "ConflictDetector", "ConflictResolver", "WorkflowOrchestrator" });
                route.WorkflowType = "conflict-resolution";
                route.Description = "Detect and resolve merge conflicts";
                route.RequiredCapabilities.AddRange(new List<string> { "write_permissions" });
                route.EstimatedDurationSeconds = 60;
                break;

            case IntentType.MergeBranches:
                route.AgentSequence.AddRange(new List<string> { "SafetyGuards", "ConflictDetector", "WorkflowOrchestrator" });
                route.WorkflowType = "merge";
                route.Description = "Merge branches";
                route.RequiredCapabilities.AddRange(new List<string> { "write_permissions", "no_merge_in_progress" });
                route.EstimatedDurationSeconds = 20;
                break;

            case IntentType.AnalyzeRepository:
                route.AgentSequence.AddRange(new List<string> { "BranchAnalyzer", "ConflictDetector" });
                route.WorkflowType = "analysis";
                route.Description = "Analyze repository branches and conflicts";
                route.RequiredCapabilities.Add("repo_access");
                route.RequiresConfirmation = false;
                route.EstimatedDurationSeconds = 15;
                break;

            case IntentType.CheckHealth:
                route.AgentSequence.AddRange(new List<string> { "BranchAnalyzer", "ConflictDetector" });
                route.WorkflowType = "health-check";
                route.Description = "Check repository health";
                route.RequiredCapabilities.Add("repo_access");
                route.RequiresConfirmation = false;
                route.EstimatedDurationSeconds = 10;
                break;

            case IntentType.ListBranches:
                route.AgentSequence.Add("BranchAnalyzer");
                route.WorkflowType = "list";
                route.Description = "List all branches";
                route.RequiredCapabilities.Add("repo_access");
                route.RequiresConfirmation = false;
                route.EstimatedDurationSeconds = 5;
                break;

            case IntentType.ResumeWorkflow:
                route.AgentSequence.Add("WorkflowOrchestrator");
                route.WorkflowType = "resume";
                route.Description = "Resume interrupted workflow";
                route.RequiredCapabilities.Add("write_permissions");
                route.EstimatedDurationSeconds = 60;
                break;

            default:
                _logger.LogWarning("Unknown intent type {IntentType}", intent.Type);
                route.Description = "Unknown intent";
                route.EstimatedDurationSeconds = 0;
                break;
        }

        // Populate agent inputs from intent parameters
        foreach (var agent in route.AgentSequence)
        {
            route.AgentInputs[agent] = new Dictionary<string, object>(intent.Parameters);
        }

        _logger.LogInformation(
            "Route determined: Agents={AgentCount}, Duration~{Duration}s",
            route.AgentSequence.Count, route.EstimatedDurationSeconds);

        return await Task.FromResult(route);
    }

    /// <summary>
    /// Validate intent before execution.
    /// </summary>
    public async Task<PreFlightResult> ValidateIntentAsync(Intent intent, string repoPath)
    {
        _logger.LogInformation("Validating intent {IntentType}", intent.Type);

        var result = new PreFlightResult { IsValid = true };

        // Check confidence level
        if (intent.ConfidenceLevel < 0.5)
        {
            result = result.WithWarnings(
                $"Low confidence parse (confidence: {intent.ConfidenceLevel:P}). Please rephrase for clarity.");
        }

        // Validate based on intent type
        if (intent.Type == IntentType.DeleteStaleBranches)
        {
            result = ValidateDeleteStaleBranches(intent, result);
        }
        else if (intent.Type == IntentType.ResolveConflicts)
        {
            result = ValidateResolveConflicts(intent, result);
        }

        // Use SafetyGuards for additional validation
        var guardResult = await _safetyGuards.ValidateIntentAsync(intent.RawInput, repoPath);
        if (!guardResult.IsValid)
        {
            result.IsValid = false;
            result = result.WithBlockers(guardResult.Blockers.ToArray());
        }

        result = result.WithWarnings(guardResult.Warnings.ToArray());

        _logger.LogInformation("Intent validation result: Valid={IsValid}", result.IsValid);
        return result;
    }

    // Helper methods

    private IntentType DetermineIntentType(string input, out double confidence)
    {
        input = input.ToLowerInvariant();
        confidence = 1.0;

        // Delete stale branches
        if (ContainsKeywords(input, "delete", "stale", "branches"))
        {
            return IntentType.DeleteStaleBranches;
        }
        if (ContainsKeywords(input, "clean", "branches") && !input.Contains("conflict"))
        {
            confidence = 0.7;
            return IntentType.DeleteStaleBranches;
        }
        if (ContainsKeywords(input, "remove", "old", "branches"))
        {
            confidence = 0.8;
            return IntentType.DeleteStaleBranches;
        }

        // Resolve conflicts
        if (ContainsKeywords(input, "resolve", "conflict"))
        {
            return IntentType.ResolveConflicts;
        }
        if (ContainsKeywords(input, "fix", "merge", "conflict"))
        {
            confidence = 0.8;
            return IntentType.ResolveConflicts;
        }

        // Merge branches
        if (ContainsKeywords(input, "merge", "branch") && !input.Contains("conflict"))
        {
            return IntentType.MergeBranches;
        }

        // Analyze repository
        if (ContainsKeywords(input, "analyze", "repository") || input.Contains("analyze repo"))
        {
            return IntentType.AnalyzeRepository;
        }
        if (ContainsKeywords(input, "show", "branches") || input.Contains("list branches"))
        {
            return IntentType.ListBranches;
        }

        // Check health
        if (ContainsKeywords(input, "health") || input.Contains("check health"))
        {
            return IntentType.CheckHealth;
        }

        // Resume workflow
        if (ContainsKeywords(input, "resume", "workflow") || input.Contains("resume from"))
        {
            return IntentType.ResumeWorkflow;
        }

        confidence = 0.3;
        return IntentType.DeleteStaleBranches; // Default fallback
    }

    private void ExtractParameters(string input, Intent intent)
    {
        // Extract days threshold
        var daysMatch = Regex.Match(input, @"(\d+)\s*days?", RegexOptions.IgnoreCase);
        if (daysMatch.Success && int.TryParse(daysMatch.Groups[1].Value, out var days))
        {
            intent.SetParameter("threshold_days", days);
        }

        // Extract quoted branch names
        var branchMatches = Regex.Matches(input, @"'([^']+)'|""([^""]+)""");
        if (branchMatches.Count > 0)
        {
            var branches = new List<string>();
            foreach (Match match in branchMatches)
            {
                var branch = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    branches.Add(branch);
                }
            }
            if (branches.Count > 0)
            {
                intent.SetParameter("target_branches", branches);
            }
        }

        // Extract merge status
        if (input.Contains("merged"))
        {
            intent.SetParameter("merge_status", "merged");
        }
        else if (input.Contains("unmerged"))
        {
            intent.SetParameter("merge_status", "unmerged");
        }

        // Extract author
        var authorMatch = Regex.Match(input, @"by\s+([a-zA-Z0-9_\.@-]+)", RegexOptions.IgnoreCase);
        if (authorMatch.Success)
        {
            intent.SetParameter("author", authorMatch.Groups[1].Value);
        }

        // Extract exceptions/exclusions
        if (input.Contains("except"))
        {
            var exceptMatch = Regex.Match(input, @"except\s+(.+?)(?:\.|$)", RegexOptions.IgnoreCase);
            if (exceptMatch.Success)
            {
                intent.SetParameter("excluded_branches", exceptMatch.Groups[1].Value);
            }
        }
    }

    private PreFlightResult ValidateDeleteStaleBranches(Intent intent, PreFlightResult result)
    {
        if (intent.GetParameter<int>("threshold_days", -1) is > 1000)
        {
            result = result.WithWarnings("Threshold >1000 days may never match branches");
        }

        return result;
    }

    private PreFlightResult ValidateResolveConflicts(Intent intent, PreFlightResult result)
    {
        // Future: add conflict-specific validation
        return result;
    }

    private string GenerateSummary(Intent intent)
    {
        return intent.Type switch
        {
            IntentType.DeleteStaleBranches =>
                $"Delete branches older than {intent.GetParameter("threshold_days", 90)} days",
            IntentType.ResolveConflicts =>
                "Detect and resolve merge conflicts",
            IntentType.MergeBranches =>
                "Merge branches",
            IntentType.AnalyzeRepository =>
                "Analyze repository branches and conflicts",
            IntentType.CheckHealth =>
                "Check repository health",
            IntentType.ListBranches =>
                "List all branches",
            IntentType.ResumeWorkflow =>
                "Resume interrupted workflow",
            _ => "Unknown intent"
        };
    }

    private static bool ContainsKeywords(string input, params string[] keywords)
    {
        return keywords.All(kw => input.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }
}
