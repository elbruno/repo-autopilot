namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Metadata for a commit in the repository.
/// </summary>
public class CommitMetadata
{
    /// <summary>
    /// Full commit SHA hash.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Short commit SHA hash (7 characters).
    /// </summary>
    public string ShortHash { get; set; } = string.Empty;

    /// <summary>
    /// Author's name.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Author's email address.
    /// </summary>
    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>
    /// Commit timestamp.
    /// </summary>
    public DateTime CommitDate { get; set; }

    /// <summary>
    /// Commit message subject line.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Full commit message (subject + body).
    /// </summary>
    public string FullMessage { get; set; } = string.Empty;

    /// <summary>
    /// Number of parent commits (0 = initial, 1 = regular, 2+ = merge).
    /// </summary>
    public int ParentCount { get; set; }
}
