namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Conflict information extracted from a merge conflict.
/// </summary>
public class ConflictInfo
{
    /// <summary>
    /// Path to the file with the conflict (relative to repository root).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// All conflict markers detected in this file.
    /// </summary>
    public List<ConflictMarker> ConflictMarkers { get; set; } = new();

    /// <summary>
    /// Complexity classification of this conflict.
    /// </summary>
    public Interfaces.ConflictComplexity Complexity { get; set; } = Interfaces.ConflictComplexity.Simple;

    /// <summary>
    /// Numerical complexity score (0-100).
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Suggested resolution strategy or notes for the developer.
    /// </summary>
    public string ResolutionSuggestion { get; set; } = string.Empty;

    /// <summary>
    /// Total number of lines in conflict regions.
    /// </summary>
    public int TotalConflictLines { get; set; }

    /// <summary>
    /// Number of non-whitespace changes in conflict regions.
    /// </summary>
    public int NonWhitespaceChanges { get; set; }
}

/// <summary>
/// A single conflict marker within a file.
/// </summary>
public class ConflictMarker
{
    /// <summary>
    /// Starting line number of the conflict (1-indexed).
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// Ending line number of the conflict (1-indexed, inclusive).
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Content in the "ours" (current) section (between <<<<<<< and =======).
    /// </summary>
    public string OurContent { get; set; } = string.Empty;

    /// <summary>
    /// Content in the "theirs" (incoming) section (between ======= and >>>>>>>).
    /// </summary>
    public string TheirContent { get; set; } = string.Empty;

    /// <summary>
    /// The branch/ref label from the marker (after <<<<<<< and >>>>>>>).
    /// </summary>
    public string OurLabel { get; set; } = "HEAD";
    public string TheirLabel { get; set; } = "BRANCH";

    /// <summary>
    /// Number of lines in "ours" section.
    /// </summary>
    public int OurLineCount => OurContent.Split('\n', System.StringSplitOptions.None).Length - 1;

    /// <summary>
    /// Number of lines in "theirs" section.
    /// </summary>
    public int TheirLineCount => TheirContent.Split('\n', System.StringSplitOptions.None).Length - 1;
}
