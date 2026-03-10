namespace LocalRepoAuto.Core.Exceptions;

/// <summary>
/// Base exception for LocalRepoAuto operations.
/// </summary>
public class LocalRepoAutoException : Exception
{
    public LocalRepoAutoException(string message) : base(message) { }
    public LocalRepoAutoException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a repository is invalid or inaccessible.
/// </summary>
public class RepositoryException : LocalRepoAutoException
{
    public RepositoryException(string message) : base(message) { }
    public RepositoryException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the repository is corrupted or .git directory is missing.
/// </summary>
public class RepositoryCorruptedException : RepositoryException
{
    public RepositoryCorruptedException(string message) : base(message) { }
    public RepositoryCorruptedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when repository is in an invalid state for the operation.
/// </summary>
public class InvalidRepositoryException : RepositoryException
{
    public InvalidRepositoryException(string message) : base(message) { }
}

/// <summary>
/// Thrown when repository is locked by another process.
/// </summary>
public class RepositoryInUseException : RepositoryException
{
    public RepositoryInUseException(string message) : base(message) { }
}

/// <summary>
/// Base for Git operation failures.
/// </summary>
public class GitOperationException : LocalRepoAutoException
{
    public GitOperationException(string message) : base(message) { }
    public GitOperationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a Git reference (branch, tag, commit) doesn't exist.
/// </summary>
public class InvalidRefException : GitOperationException
{
    public string? Reference { get; }

    public InvalidRefException(string reference) : base($"Invalid reference: {reference}")
    {
        Reference = reference;
    }
}

/// <summary>
/// Thrown when trying to merge or delete with unmerged changes.
/// </summary>
public class UnmergedPathsException : GitOperationException
{
    public List<string> UnmergedFiles { get; }

    public UnmergedPathsException(List<string> unmergedFiles) 
        : base($"Repository has unmerged paths: {string.Join(", ", unmergedFiles)}")
    {
        UnmergedFiles = unmergedFiles;
    }
}

/// <summary>
/// Thrown when user lacks permission for an operation.
/// </summary>
public class PermissionDeniedException : GitOperationException
{
    public PermissionDeniedException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a branch cannot be deleted due to protection rules.
/// </summary>
public class ProtectedBranchException : PermissionDeniedException
{
    public string? BranchName { get; }

    public ProtectedBranchException(string branchName) 
        : base($"Cannot delete protected branch: {branchName}")
    {
        BranchName = branchName;
    }
}

/// <summary>
/// Thrown when a merge has conflicts that cannot be auto-resolved.
/// </summary>
public class ConflictedException : GitOperationException
{
    public List<string> ConflictingFiles { get; }

    public ConflictedException(List<string> conflictingFiles) 
        : base($"Merge has conflicts in: {string.Join(", ", conflictingFiles)}")
    {
        ConflictingFiles = conflictingFiles;
    }
}

/// <summary>
/// Configuration-related errors.
/// </summary>
public class ConfigurationException : LocalRepoAutoException
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
