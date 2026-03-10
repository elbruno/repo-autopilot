namespace LocalRepoAuto.Core.Models;

/// <summary>
/// Log entry for a Git operation performed by the system.
/// </summary>
public class GitOperationLog
{
    /// <summary>
    /// When the operation was executed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Name of the Git operation (e.g., "git branch -a", "git log").
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Command-line arguments passed to git.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Standard output from the git command.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Exit code from the git process.
    /// </summary>
    public int ExitCode { get; set; }
}
