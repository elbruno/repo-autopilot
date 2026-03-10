namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Enum for merge strategies that can be used to resolve conflicts.
/// </summary>
public enum ResolutionStrategy
{
    /// <summary>
    /// Apply both changes using 3-way merge with semantic intent preservation.
    /// Safe for non-overlapping changes. Can handle some semantic conflicts.
    /// </summary>
    Recursive,

    /// <summary>
    /// Keep "ours" version, discard "theirs" entirely.
    /// Safest option but loses all incoming changes.
    /// </summary>
    ResolveOurs,

    /// <summary>
    /// Keep "theirs" version, discard "ours" entirely.
    /// Safest option but loses all our changes.
    /// </summary>
    ResolveTheirs,

    /// <summary>
    /// Conflict Resolution Transport - structured 3-way merge algorithm.
    /// Most complex but preserves intent from both sides.
    /// </summary>
    ORT,

    /// <summary>
    /// Conflict is too complex or ambiguous for automatic resolution.
    /// Requires manual human review and decision.
    /// </summary>
    RequiresHumanReview
}

/// <summary>
/// Categorizes semantic conflict types for strategy selection.
/// </summary>
public enum SemanticConflictType
{
    /// <summary>
    /// Only whitespace differences (spaces, tabs, blank lines).
    /// Very low complexity, safe to auto-resolve.
    /// </summary>
    Whitespace,

    /// <summary>
    /// Non-overlapping line changes that don't affect each other.
    /// Medium complexity, usually safe to merge.
    /// </summary>
    NonOverlapping,

    /// <summary>
    /// Both sides modified the same variable, method, or code region.
    /// Semantic intent may conflict. Requires understanding.
    /// </summary>
    SemanticConflict,

    /// <summary>
    /// One side deleted content, other modified it.
    /// High complexity - deletion intent unclear.
    /// </summary>
    DeletionVsModification,

    /// <summary>
    /// Method/function signature changed (parameters, return type, etc).
    /// High complexity due to cascading impact.
    /// </summary>
    SignatureChange,

    /// <summary>
    /// Import/using directive changes (module dependencies).
    /// Medium complexity but affects code availability.
    /// </summary>
    ImportConflict,

    /// <summary>
    /// Conflict type not yet analyzed or unknown.
    /// </summary>
    Unknown
}

/// <summary>
/// Result of validating a resolved conflict.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed (content is syntactically/semantically valid).
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error messages if validation failed.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warnings (e.g., potential issues but not blockers).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Details of validation performed (e.g., "syntax check passed", "compile check passed").
    /// </summary>
    public List<string> ValidationDetails { get; set; } = new();
}

/// <summary>
/// Represents a single suggested conflict resolution.
/// </summary>
public class ConflictProposal
{
    /// <summary>
    /// Unique identifier for this proposal (e.g., "prop-001").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The resolved content with conflict markers removed.
    /// </summary>
    public string ResolvedContent { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of how the conflict was resolved.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Merge strategy used to generate this proposal.
    /// </summary>
    public ResolutionStrategy Strategy { get; set; } = ResolutionStrategy.RequiresHumanReview;

    /// <summary>
    /// Confidence score (0.0–1.0) that this is the intended resolution.
    /// 0.85+ = High confidence (Copilot or clear heuristic).
    /// 0.60–0.85 = Medium confidence (requires review).
    /// &lt;0.60 = Low confidence (likely needs human review).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Potential risks or side effects of accepting this proposal.
    /// E.g., "may lose variable definition", "assumes intent from Copilot".
    /// </summary>
    public List<string> Risks { get; set; } = new();

    /// <summary>
    /// Whether this proposal can be validated (e.g., syntax check, compile check).
    /// </summary>
    public bool Validatable { get; set; }

    /// <summary>
    /// Result of validation (if Validatable = true).
    /// </summary>
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// Audit trail: explanation of why this proposal was generated.
    /// Includes conflict classification, strategy selection logic, etc.
    /// </summary>
    public string AuditLog { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when proposal was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request envelope for conflict resolution.
/// </summary>
public class ResolutionRequest
{
    /// <summary>
    /// Conflict information from Phase 2.
    /// </summary>
    public ConflictInfo Conflict { get; set; } = new();

    /// <summary>
    /// Path to the conflicted file (relative to repo root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Base branch (merge target).
    /// </summary>
    public string BaseBranch { get; set; } = string.Empty;

    /// <summary>
    /// Head branch (branch being merged).
    /// </summary>
    public string HeadBranch { get; set; } = string.Empty;

    /// <summary>
    /// File content from base branch.
    /// </summary>
    public string BaseContent { get; set; } = string.Empty;

    /// <summary>
    /// File content from head branch.
    /// </summary>
    public string HeadContent { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use Copilot SDK for analysis (if available).
    /// </summary>
    public bool AllowCopilotAnalysis { get; set; } = true;

    /// <summary>
    /// Maximum confidence threshold for auto-resolution (0.0–1.0).
    /// Proposals below this require human review.
    /// </summary>
    public double MinimumConfidenceForAutoResolve { get; set; } = 0.85;
}

/// <summary>
/// Response envelope for conflict resolution.
/// </summary>
public class ResolutionResponse
{
    /// <summary>
    /// List of proposed resolutions, ranked by confidence.
    /// </summary>
    public List<ConflictProposal> Proposals { get; set; } = new();

    /// <summary>
    /// Recommended proposal (if any) based on confidence.
    /// </summary>
    public ConflictProposal? RecommendedProposal { get; set; }

    /// <summary>
    /// Overall audit log for resolution process.
    /// </summary>
    public string ProcessAuditLog { get; set; } = string.Empty;

    /// <summary>
    /// Whether automatic resolution was possible.
    /// </summary>
    public bool CanAutoResolve => RecommendedProposal?.Confidence >= 0.85;

    /// <summary>
    /// Timestamp when resolution attempt was completed.
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
