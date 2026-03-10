namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Context information extracted from conflict analysis.
/// Includes semantic understanding and classification.
/// </summary>
public class ConflictContext
{
    /// <summary>
    /// Type of conflict detected (semantic, whitespace, etc).
    /// </summary>
    public SemanticConflictType Type { get; set; } = SemanticConflictType.Unknown;

    /// <summary>
    /// Number of lines affected in the conflict.
    /// </summary>
    public int LinesAffected { get; set; }

    /// <summary>
    /// Semantic intent of the "ours" side (e.g., "refactor variable names").
    /// </summary>
    public string OurIntent { get; set; } = string.Empty;

    /// <summary>
    /// Semantic intent of the "theirs" side (e.g., "add type annotation").
    /// </summary>
    public string TheirIntent { get; set; } = string.Empty;

    /// <summary>
    /// Whether Copilot SDK was used for analysis.
    /// </summary>
    public bool CopilotAnalyzed { get; set; }

    /// <summary>
    /// Confidence that the analysis is correct (0.0–1.0).
    /// Higher confidence = more reliable semantic understanding.
    /// </summary>
    public double AnalysisConfidence { get; set; }

    /// <summary>
    /// Detailed analysis notes from Copilot or heuristics.
    /// </summary>
    public string AnalysisNotes { get; set; } = string.Empty;
}
