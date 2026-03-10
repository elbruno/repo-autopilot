using LocalRepoAuto.Core.Models;
using LocalRepoAuto.Core.Safety;

namespace LocalRepoAuto.Core.Orchestration;

/// <summary>
/// Intent router: parse developer intent, extract parameters, validate, route to agents.
/// </summary>
public interface IIntentRouter
{
    /// <summary>
    /// Parse developer intent from natural language input.
    /// Extracts intent type, parameters, and confidence level.
    /// </summary>
    Task<Intent> ParseIntentAsync(string userInput);

    /// <summary>
    /// Route a parsed intent to appropriate agent(s).
    /// Determines which agents should be invoked and in what order.
    /// </summary>
    Task<WorkflowRoute> RouteToAgentAsync(Intent intent);

    /// <summary>
    /// Validate intent against safety guards before execution.
    /// Checks for contradictions, extreme parameters, protected branch targeting.
    /// </summary>
    Task<PreFlightResult> ValidateIntentAsync(Intent intent, string repoPath);
}

/// <summary>
/// Routing information: which agents to call and in what order.
/// </summary>
public class WorkflowRoute
{
    /// <summary>
    /// Sequence of agents to invoke.
    /// Examples: ["BranchAnalyzer"], ["ConflictDetector", "ConflictResolver"]
    /// </summary>
    public List<string> AgentSequence { get; set; } = new();

    /// <summary>
    /// Input parameters for each agent.
    /// Key: agent name, Value: parameters dict
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> AgentInputs { get; set; } = new();

    /// <summary>
    /// Estimated duration in seconds (rough estimate).
    /// </summary>
    public int EstimatedDurationSeconds { get; set; } = 0;

    /// <summary>
    /// Required capabilities for this workflow to execute.
    /// Examples: ["write_permissions", "repo_access", "no_merge_in_progress"]
    /// </summary>
    public List<string> RequiredCapabilities { get; set; } = new();

    /// <summary>
    /// Human-readable workflow description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether user confirmation is required before execution.
    /// </summary>
    public bool RequiresConfirmation { get; set; } = true;

    /// <summary>
    /// Workflow type being executed.
    /// </summary>
    public string WorkflowType { get; set; } = string.Empty;
}
