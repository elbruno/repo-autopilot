using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Logging;
using LocalRepoAuto.Core.Models;
using LocalRepoAuto.Core.Reporting;
using LocalRepoAuto.Core.Safety;
using LocalRepoAuto.Core.State;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Workflows;

/// <summary>
/// Implementation of workflow orchestrator.
/// Coordinates multi-step workflows with state checkpoints and audit logging.
/// </summary>
public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IBranchAnalyzer _branchAnalyzer;
    private readonly IConflictDetector _conflictDetector;
    private readonly IPreFlightChecker _safetyGuards;
    private readonly IAuditLogger _auditLogger;
    private readonly IStateManager _stateManager;
    private readonly IResultReporter _resultReporter;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(
        IBranchAnalyzer branchAnalyzer,
        IConflictDetector conflictDetector,
        IPreFlightChecker safetyGuards,
        IAuditLogger auditLogger,
        IStateManager stateManager,
        IResultReporter resultReporter,
        ILogger<WorkflowOrchestrator> logger)
    {
        _branchAnalyzer = branchAnalyzer;
        _conflictDetector = conflictDetector;
        _safetyGuards = safetyGuards;
        _auditLogger = auditLogger;
        _stateManager = stateManager;
        _resultReporter = resultReporter;
        _logger = logger;
    }

    /// <summary>
    /// Execute cleanup workflow.
    /// </summary>
    public async Task<WorkflowOutcome> ExecuteCleanupWorkflowAsync(Intent intent, string repoPath)
    {
        var outcome = new WorkflowOutcome
        {
            OriginalIntent = intent,
            RepoPath = repoPath,
            Status = WorkflowStatus.InProgress
        };

        try
        {
            _logger.LogInformation("Starting cleanup workflow {WorkflowId}", outcome.WorkflowId);

            // Step 1: Pre-flight checks
            var preflight = await _safetyGuards.CheckRepositoryStateAsync(repoPath);
            if (!preflight.IsValid)
            {
                outcome.Status = WorkflowStatus.Failed;
                outcome.AddError($"Pre-flight check failed: {string.Join(", ", preflight.Blockers)}");
                return outcome;
            }

            // Log intent
            await _auditLogger.LogIntentAsync(intent);

            // Step 2: Analyze branches
            _logger.LogInformation("Analyzing branches...");
            var branches = await _branchAnalyzer.ListBranchesAsync();
            var step2 = new WorkflowStep
            {
                Agent = "BranchAnalyzer",
                Action = "ListBranches",
                Output = new() { { "branch_count", branches.Count } },
                Status = "Success"
            };
            outcome.Steps.Add(step2);

            // Save checkpoint
            var checkpoint1 = new WorkflowCheckpoint
            {
                WorkflowId = outcome.WorkflowId,
                Label = "branches-analyzed",
                RepoPath = repoPath,
                OriginalIntent = intent,
                StepNumber = 1,
                State = new() { { "branches", SerializeBranches(branches) } }
            };
            await _stateManager.SaveCheckpointAsync(checkpoint1);

            // Step 3: Filter candidates
            _logger.LogInformation("Filtering stale branches...");
            var staleBranches = await _branchAnalyzer.DetectStaleBranchesAsync(
                intent.GetParameter<int>("threshold_days", 90));

            var candidates = FilterCandidates(staleBranches, intent);
            var step3 = new WorkflowStep
            {
                Agent = "WorkflowOrchestrator",
                Action = "FilterCandidates",
                Output = new() { { "candidates_count", candidates.Count } },
                Status = "Success"
            };
            outcome.Steps.Add(step3);

            // Save checkpoint
            var checkpoint2 = new WorkflowCheckpoint
            {
                WorkflowId = outcome.WorkflowId,
                Label = "candidates-filtered",
                RepoPath = repoPath,
                OriginalIntent = intent,
                StepNumber = 2,
                State = new() { { "candidates", SerializeBranches(candidates) } }
            };
            await _stateManager.SaveCheckpointAsync(checkpoint2);

            // Step 4: Log proposal
            _logger.LogInformation("Preparing proposal for {CandidateCount} branches", candidates.Count);

            // Record results
            outcome.SetResult("branches_deleted", candidates.Count);
            outcome.SetResult("branches_kept", branches.Count - candidates.Count);
            outcome.SetResult("deleted_branches", SerializeBranchesForReport(candidates));

            var step4 = new WorkflowStep
            {
                Agent = "WorkflowOrchestrator",
                Action = "PrepareProposal",
                Output = new()
                {
                    { "proposal_generated", true },
                    { "candidates_count", candidates.Count }
                },
                Status = "Success"
            };
            outcome.Steps.Add(step4);

            // Save checkpoint
            var checkpoint3 = new WorkflowCheckpoint
            {
                WorkflowId = outcome.WorkflowId,
                Label = "proposal-generated",
                RepoPath = repoPath,
                OriginalIntent = intent,
                StepNumber = 3,
                State = new() { { "candidates", SerializeBranches(candidates) } }
            };
            await _stateManager.SaveCheckpointAsync(checkpoint3);

            // Log outcome
            var outcomeEntry = new AuditLogEntry
            {
                WorkflowId = outcome.WorkflowId,
                Actor = "WorkflowOrchestrator",
                Action = "CleanupWorkflowCompleted",
                Status = "Completed",
                Results = new()
                {
                    { "branches_deleted", candidates.Count },
                    { "branches_kept", branches.Count - candidates.Count }
                }
            };
            await _auditLogger.LogOutcomeAsync(outcomeEntry);

            outcome.Status = WorkflowStatus.Completed;
            outcome.CompletedAt = DateTime.UtcNow;
            outcome.AuditTrailId = _auditLogger.GetAuditFilePath(outcome.WorkflowId);

            _logger.LogInformation(
                "Cleanup workflow completed successfully: {DeletedCount} branches to delete",
                candidates.Count);

            return outcome;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup workflow failed");
            outcome.Status = WorkflowStatus.Failed;
            outcome.AddError($"Workflow failed: {ex.Message}");
            outcome.CompletedAt = DateTime.UtcNow;
            return outcome;
        }
    }

    /// <summary>
    /// Execute conflict resolution workflow.
    /// </summary>
    public async Task<WorkflowOutcome> ExecuteConflictResolutionAsync(Intent intent, string repoPath)
    {
        var outcome = new WorkflowOutcome
        {
            OriginalIntent = intent,
            RepoPath = repoPath,
            Status = WorkflowStatus.InProgress
        };

        try
        {
            _logger.LogInformation("Starting conflict resolution workflow {WorkflowId}", outcome.WorkflowId);

            // Pre-flight checks
            var preflight = await _safetyGuards.CheckRepositoryStateAsync(repoPath);
            if (!preflight.IsValid)
            {
                outcome.Status = WorkflowStatus.Failed;
                outcome.AddError($"Pre-flight check failed");
                return outcome;
            }

            // Log intent
            await _auditLogger.LogIntentAsync(intent);

            // Detect conflicts
            _logger.LogInformation("Detecting merge conflicts...");
            var conflicts = await _conflictDetector.DetectConflictsAsync("HEAD", "MERGE_HEAD");

            var step1 = new WorkflowStep
            {
                Agent = "ConflictDetector",
                Action = "DetectConflicts",
                Output = new() { { "conflict_count", conflicts.Count } },
                Status = "Success"
            };
            outcome.Steps.Add(step1);

            // Analyze complexity
            var simpleCount = conflicts.Count(c => c.Complexity == Interfaces.ConflictComplexity.Simple);
            var mediumCount = conflicts.Count(c => c.Complexity == Interfaces.ConflictComplexity.Medium);
            var complexCount = conflicts.Count(c => c.Complexity == Interfaces.ConflictComplexity.Complex);

            outcome.SetResult("conflicts_detected", conflicts.Count);
            outcome.SetResult("conflicts_simple", simpleCount);
            outcome.SetResult("conflicts_medium", mediumCount);
            outcome.SetResult("conflicts_complex", complexCount);

            // Log outcome
            var outcomeEntry = new AuditLogEntry
            {
                WorkflowId = outcome.WorkflowId,
                Actor = "WorkflowOrchestrator",
                Action = "ConflictResolutionWorkflowCompleted",
                Status = "Completed",
                Results = new()
                {
                    { "conflicts_detected", conflicts.Count },
                    { "simple", simpleCount },
                    { "medium", mediumCount },
                    { "complex", complexCount }
                }
            };
            await _auditLogger.LogOutcomeAsync(outcomeEntry);

            outcome.Status = WorkflowStatus.Completed;
            outcome.CompletedAt = DateTime.UtcNow;
            outcome.AuditTrailId = _auditLogger.GetAuditFilePath(outcome.WorkflowId);

            _logger.LogInformation(
                "Conflict resolution analysis completed: {SimpleCount} simple, {MediumCount} medium, {ComplexCount} complex",
                simpleCount, mediumCount, complexCount);

            return outcome;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conflict resolution workflow failed");
            outcome.Status = WorkflowStatus.Failed;
            outcome.AddError($"Workflow failed: {ex.Message}");
            outcome.CompletedAt = DateTime.UtcNow;
            return outcome;
        }
    }

    /// <summary>
    /// Execute health check workflow (read-only).
    /// </summary>
    public async Task<WorkflowOutcome> ExecuteRepositoryHealthAsync(Intent intent, string repoPath)
    {
        var outcome = new WorkflowOutcome
        {
            OriginalIntent = intent,
            RepoPath = repoPath,
            Status = WorkflowStatus.InProgress
        };

        try
        {
            _logger.LogInformation("Starting health check workflow {WorkflowId}", outcome.WorkflowId);

            // Analyze branches
            var branches = await _branchAnalyzer.ListBranchesAsync();
            var staleBranches = branches.Where(b => !b.IsProtected && b.StalenessScore > 0).ToList();
            var protectedBranches = branches.Where(b => b.IsProtected).ToList();

            // Detect conflicts
            var conflicts = await _conflictDetector.DetectConflictsAsync("HEAD", "MERGE_HEAD");

            // Calculate health score
            var healthScore = 100;
            if (staleBranches.Count > 10) healthScore -= 15;
            if (staleBranches.Count > 5) healthScore -= 5;
            if (conflicts.Count > 0) healthScore -= 10;

            healthScore = Math.Max(0, Math.Min(100, healthScore));

            // Set results
            outcome.SetResult("total_branches", branches.Count);
            outcome.SetResult("stale_branches", staleBranches.Count);
            outcome.SetResult("protected_branches", protectedBranches.Count);
            outcome.SetResult("average_age_days", branches.Average(b => b.DaysStale));
            outcome.SetResult("conflicts_detected", conflicts.Count);
            outcome.SetResult("health_score", healthScore);

            outcome.Status = WorkflowStatus.Completed;
            outcome.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Health check completed: score={HealthScore}, stale={StalCount}, conflicts={ConflictCount}",
                healthScore, staleBranches.Count, conflicts.Count);

            return outcome;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check workflow failed");
            outcome.Status = WorkflowStatus.Failed;
            outcome.AddError($"Workflow failed: {ex.Message}");
            outcome.CompletedAt = DateTime.UtcNow;
            return outcome;
        }
    }

    /// <summary>
    /// Execute branch listing workflow.
    /// </summary>
    public async Task<WorkflowOutcome> ExecuteListBranchesAsync(Intent intent, string repoPath)
    {
        var outcome = new WorkflowOutcome
        {
            OriginalIntent = intent,
            RepoPath = repoPath,
            Status = WorkflowStatus.InProgress
        };

        try
        {
            _logger.LogInformation("Listing branches...");

            var branches = await _branchAnalyzer.ListBranchesAsync();
            outcome.SetResult("branches", SerializeBranchesForReport(branches));
            outcome.SetResult("branch_count", branches.Count);

            outcome.Status = WorkflowStatus.Completed;
            outcome.CompletedAt = DateTime.UtcNow;

            return outcome;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List branches workflow failed");
            outcome.Status = WorkflowStatus.Failed;
            outcome.AddError($"Workflow failed: {ex.Message}");
            outcome.CompletedAt = DateTime.UtcNow;
            return outcome;
        }
    }

    /// <summary>
    /// Rollback last action (not implemented in MVP).
    /// </summary>
    public async Task<RollbackResult> RollbackLastActionAsync(string repoPath)
    {
        _logger.LogWarning("Rollback requested but not yet implemented");

        return await Task.FromResult(new RollbackResult
        {
            Success = false,
            Summary = "Rollback not yet implemented in MVP"
        });
    }

    /// <summary>
    /// Resume from checkpoint (not implemented in MVP).
    /// </summary>
    public async Task<WorkflowOutcome> ResumeFromCheckpointAsync(string checkpointId, string repoPath)
    {
        _logger.LogWarning("Resume requested but not yet implemented");

        return await Task.FromResult(new WorkflowOutcome
        {
            Status = WorkflowStatus.Failed,
            AuditTrailId = "unknown"
        });
    }

    // Helper methods

    private List<BranchInfo> FilterCandidates(List<BranchInfo> staleBranches, Intent intent)
    {
        var candidates = staleBranches.ToList();

        // Filter by merge status if specified
        var mergeStatus = intent.GetParameter<string>("merge_status", null);
        if (mergeStatus == "merged")
        {
            // Future: filter to merged branches only
        }

        // Filter out excluded branches
        var excluded = intent.GetParameter<string>("excluded_branches", null);
        if (!string.IsNullOrWhiteSpace(excluded))
        {
            var excludedList = excluded.Split(',').Select(b => b.Trim()).ToList();
            candidates = candidates.Where(b => !excludedList.Contains(b.Name)).ToList();
        }

        return candidates;
    }

    private List<object> SerializeBranches(List<BranchInfo> branches)
    {
        return branches.Select(b => new object[] { b.Name, b.StalenessScore }).Cast<object>().ToList();
    }

    private List<Dictionary<string, object>> SerializeBranchesForReport(List<BranchInfo> branches)
    {
        return branches.Select(b => new Dictionary<string, object>
        {
            { "name", b.Name },
            { "days_stale", b.DaysStale },
            { "last_author", b.LastAuthor },
            { "last_commit_date", b.LastCommitDate.ToString("yyyy-MM-dd") },
            { "staleness_score", b.StalenessScore }
        }).ToList();
    }
}
