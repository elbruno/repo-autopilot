namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Result of a diff operation between two references.
/// </summary>
public class DiffResult
{
    /// <summary>
    /// Statistics per file (file path -> number of changed lines).
    /// </summary>
    public Dictionary<string, int> Stats { get; set; } = new();

    /// <summary>
    /// Total number of files changed.
    /// </summary>
    public int FilesChanged { get; set; }

    /// <summary>
    /// Total number of lines added.
    /// </summary>
    public int Insertions { get; set; }

    /// <summary>
    /// Total number of lines deleted.
    /// </summary>
    public int Deletions { get; set; }

    /// <summary>
    /// Raw diff output (for detailed inspection).
    /// </summary>
    public string RawDiff { get; set; } = string.Empty;
}

/// <summary>
/// Detailed statistics from diff command.
/// </summary>
public class DiffStatResult
{
    /// <summary>
    /// Per-file statistics.
    /// </summary>
    public List<FileDiffStat> Files { get; set; } = new();

    /// <summary>
    /// Total insertions across all files.
    /// </summary>
    public int TotalInsertions { get; set; }

    /// <summary>
    /// Total deletions across all files.
    /// </summary>
    public int TotalDeletions { get; set; }

    /// <summary>
    /// Total files changed.
    /// </summary>
    public int TotalFilesChanged => Files.Count;
}

/// <summary>
/// Statistics for a single file in a diff.
/// </summary>
public class FileDiffStat
{
    /// <summary>
    /// File path relative to repository root.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Type of change (Added, Deleted, Modified, Renamed, etc).
    /// </summary>
    public string ChangeType { get; set; } = "Modified";

    /// <summary>
    /// Lines added in this file.
    /// </summary>
    public int Insertions { get; set; }

    /// <summary>
    /// Lines deleted from this file.
    /// </summary>
    public int Deletions { get; set; }

    /// <summary>
    /// Total change magnitude (insertions + deletions).
    /// </summary>
    public int ChangeMagnitude => Insertions + Deletions;
}
