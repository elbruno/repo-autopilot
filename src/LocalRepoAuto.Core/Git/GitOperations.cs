using System.Diagnostics;
using System.Text;
using LocalRepoAuto.Core.Exceptions;
using LocalRepoAuto.Core.Interfaces;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalRepoAuto.Core.Git;

/// <summary>
/// Safe wrapper around Git CLI operations.
/// All operations are local-only with comprehensive logging.
/// </summary>
public class GitOperations : IGitOperations
{
    private readonly string _repositoryPath;
    private readonly ILogger<GitOperations> _logger;
    private readonly List<GitOperationLog> _operationLogs = new();

    public GitOperations(ILogger<GitOperations> logger, IOptions<RepositorySettings> options)
    {
        _logger = logger;
        _repositoryPath = options.Value.RepositoryPath ?? ".";

        // Validate repository on initialization
        ValidateRepository();
    }

    /// <summary>
    /// Ensures the repository is valid and accessible.
    /// </summary>
    private void ValidateRepository()
    {
        try
        {
            var result = ExecuteGit("rev-parse --git-dir");
            if (!result.Success)
            {
                throw new RepositoryCorruptedException("Not a valid Git repository or .git directory is inaccessible.");
            }
        }
        catch (InvalidOperationException)
        {
            throw new RepositoryCorruptedException("Git is not installed or not in PATH.");
        }
    }

    public async Task<List<string>> GetBranchesAsync()
    {
        var result = await ExecuteGitAsync("branch -a --format=%(refname:short)");
        
        if (!result.Success)
        {
            throw new GitOperationException("Failed to list branches: " + result.Error);
        }

        var branches = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        _logger.LogInformation("Listed {BranchCount} branches", branches.Count);
        return branches;
    }

    public async Task<CommitMetadata> GetCommitMetadataAsync(string reference)
    {
        // Validate reference exists
        var validateResult = await ExecuteGitAsync($"cat-file -t {reference}");
        if (!validateResult.Success)
        {
            throw new InvalidRefException(reference);
        }

        // Get commit details
        var format = "%(H)%n%(h)%n%(an)%n%(ae)%n%(ai)%n%(s)%n%(B)%n%(P)";
        var result = await ExecuteGitAsync($"log -1 --format={format} {reference}");

        if (!result.Success)
        {
            throw new InvalidRefException(reference);
        }

        var lines = result.Output.Split('\n');
        if (lines.Length < 8)
        {
            throw new GitOperationException($"Unexpected git log format for {reference}");
        }

        var parents = lines[7].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return new CommitMetadata
        {
            Hash = lines[0],
            ShortHash = lines[1],
            AuthorName = lines[2],
            AuthorEmail = lines[3],
            CommitDate = DateTime.Parse(lines[4]),
            Message = lines[5],
            FullMessage = string.Join("\n", lines, 5, Math.Max(0, lines.Length - 5)),
            ParentCount = parents.Length
        };
    }

    public async Task<string> GetCurrentBranchAsync()
    {
        var result = await ExecuteGitAsync("rev-parse --abbrev-ref HEAD");
        
        if (!result.Success)
        {
            throw new GitOperationException("Failed to get current branch: " + result.Error);
        }

        var branch = result.Output.Trim();
        return branch == "HEAD" ? "(HEAD detached)" : branch;
    }

    public async Task<DiffResult> GetDiffAsync(string baseBranch, string headBranch)
    {
        // Validate both refs exist
        await ValidateRefAsync(baseBranch);
        await ValidateRefAsync(headBranch);

        var result = await ExecuteGitAsync($"diff --stat {baseBranch}..{headBranch}");

        if (!result.Success)
        {
            throw new GitOperationException($"Failed to diff {baseBranch}..{headBranch}: {result.Error}");
        }

        var diffResult = new DiffResult { RawDiff = result.Output };

        // Parse diff stat output
        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains('|'))
            {
                var parts = line.Split('|');
                if (parts.Length >= 2)
                {
                    var file = parts[0].Trim();
                    var stats = parts[1].Trim();
                    
                    int insertions = stats.Count(c => c == '+');
                    int deletions = stats.Count(c => c == '-');

                    diffResult.Stats[file] = insertions + deletions;
                    diffResult.Insertions += insertions;
                    diffResult.Deletions += deletions;
                    diffResult.FilesChanged++;
                }
            }
        }

        return diffResult;
    }

    public async Task<DiffStatResult> GetDiffStatAsync(string baseBranch, string headBranch)
    {
        var basicDiff = await GetDiffAsync(baseBranch, headBranch);
        
        var result = new DiffStatResult
        {
            TotalInsertions = basicDiff.Insertions,
            TotalDeletions = basicDiff.Deletions
        };

        // Convert stats dictionary to file stats
        foreach (var (file, changes) in basicDiff.Stats)
        {
            // Parse changes back into insertions/deletions (simplified)
            result.Files.Add(new FileDiffStat
            {
                Path = file,
                Insertions = changes / 2,
                Deletions = changes / 2,
                ChangeType = "Modified"
            });
        }

        return result;
    }

    public async Task<string> GetCommonAncestorAsync(string ref1, string ref2)
    {
        await ValidateRefAsync(ref1);
        await ValidateRefAsync(ref2);

        var result = await ExecuteGitAsync($"merge-base {ref1} {ref2}");

        if (!result.Success)
        {
            throw new GitOperationException($"Failed to find common ancestor of {ref1} and {ref2}: {result.Error}");
        }

        return result.Output.Trim();
    }

    public async Task<bool> CanDeleteBranchAsync(string branchName)
    {
        // Check if branch is protected
        if (await IsProtectedBranchAsync(branchName))
        {
            return false;
        }

        // Check if fully merged to main
        var current = await GetCurrentBranchAsync();
        
        // If we're on the branch to delete, we can't delete it
        if (current == branchName)
        {
            return false;
        }

        // Check if fully merged to main/master
        var mainBranch = (await BranchExistsAsync("main")) ? "main" : "master";
        if (!(await BranchExistsAsync(mainBranch)))
        {
            // Can't verify merge status without main/master
            return true;
        }

        var result = await ExecuteGitAsync($"branch --merged {mainBranch} | grep {branchName}");
        return result.Success; // If grep found it, it's merged and can be deleted
    }

    public async Task DeleteBranchAsync(string branchName, bool force = false)
    {
        if (!force && !(await CanDeleteBranchAsync(branchName)))
        {
            if (await IsProtectedBranchAsync(branchName))
            {
                throw new ProtectedBranchException(branchName);
            }

            throw new GitOperationException($"Branch {branchName} has unmerged commits. Use force=true to delete anyway.");
        }

        var flag = force ? "-D" : "-d";
        var result = await ExecuteGitAsync($"branch {flag} {branchName}");

        if (!result.Success)
        {
            throw new GitOperationException($"Failed to delete branch {branchName}: {result.Error}");
        }

        _logger.LogInformation("Deleted branch {BranchName}", branchName);
    }

    public async Task<MergeResult> MergeBranchesAsync(string baseBranch, string headBranch, string strategy = "recursive")
    {
        await ValidateRefAsync(baseBranch);
        await ValidateRefAsync(headBranch);

        // Perform dry-run merge on detached HEAD
        var tempBranch = "detached-merge-test-" + Guid.NewGuid().ToString().Substring(0, 8);

        try
        {
            // Checkout base as detached
            var checkoutResult = await ExecuteGitAsync($"checkout --detach {baseBranch}");
            if (!checkoutResult.Success)
            {
                throw new GitOperationException($"Failed to checkout {baseBranch}: {checkoutResult.Error}");
            }

            // Attempt merge
            var mergeResult = await ExecuteGitAsync($"merge --no-commit --no-ff {headBranch} -s {strategy}");

            var result = new MergeResult
            {
                IsDryRun = true,
                Success = mergeResult.Success
            };

            if (!mergeResult.Success)
            {
                // Check for conflicts
                var statusResult = await ExecuteGitAsync("status --porcelain");
                var conflictFiles = statusResult.Output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.StartsWith("UU") || line.StartsWith("AA") || line.StartsWith("DD"))
                    .Select(line => line.Substring(3).Trim())
                    .ToList();

                result.Conflicts = await ExtractConflictsAsync(conflictFiles);
                result.Error = mergeResult.Error;
            }

            // Abort the merge (since it's a dry-run)
            await ExecuteGitAsync("merge --abort");

            return result;
        }
        finally
        {
            // Return to original branch
            var originalBranch = await GetCurrentBranchAsync();
            if (originalBranch == "(HEAD detached)")
            {
                await ExecuteGitAsync("checkout -");
            }
        }
    }

    public List<GitOperationLog> GetOperationLogs()
    {
        return _operationLogs.ToList();
    }

    public void ClearOperationLogs()
    {
        _operationLogs.Clear();
    }

    // ============ Private Helpers ============

    private GitOperationLog ExecuteGit(string arguments)
    {
        var stopwatch = Stopwatch.StartNew();
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start git process");
                }

                process.OutputDataReceived += (_, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        error.AppendLine(e.Data);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(30000))
                {
                    process.Kill();
                    throw new TimeoutException("Git command timed out after 30 seconds");
                }

                stopwatch.Stop();

                var log = new GitOperationLog
                {
                    Operation = "git " + arguments.Split(' ')[0],
                    Arguments = arguments,
                    Success = process.ExitCode == 0,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ExitCode = process.ExitCode,
                    Timestamp = DateTime.UtcNow
                };

                _operationLogs.Add(log);
                _logger.LogDebug("Git {Operation} {Arguments} - Exit: {ExitCode} in {Duration}ms",
                    log.Operation, log.Arguments, log.ExitCode, log.DurationMs);

                return log;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var log = new GitOperationLog
            {
                Operation = "git " + arguments.Split(' ')[0],
                Arguments = arguments,
                Success = false,
                Error = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            _operationLogs.Add(log);
            _logger.LogError(ex, "Git operation failed: {Operation} {Arguments}", log.Operation, log.Arguments);
            throw;
        }
    }

    private async Task<GitOperationLog> ExecuteGitAsync(string arguments)
    {
        return await Task.Run(() => ExecuteGit(arguments));
    }

    private async Task ValidateRefAsync(string reference)
    {
        var result = await ExecuteGitAsync($"cat-file -t {reference}");
        if (!result.Success)
        {
            throw new InvalidRefException(reference);
        }
    }

    private async Task<bool> BranchExistsAsync(string branchName)
    {
        var result = await ExecuteGitAsync($"rev-parse --verify {branchName}");
        return result.Success;
    }

    private async Task<bool> IsProtectedBranchAsync(string branchName)
    {
        var protectedPatterns = new[] { "main", "master", "develop", "staging", "production" };
        
        // Check exact matches
        if (protectedPatterns.Contains(branchName))
            return true;

        // Check patterns
        if (branchName.StartsWith("release/"))
            return true;

        // In Phase 3, read from config
        return false;
    }

    private async Task<List<ConflictInfo>> ExtractConflictsAsync(List<string> conflictFiles)
    {
        var conflicts = new List<ConflictInfo>();

        foreach (var file in conflictFiles)
        {
            var result = await ExecuteGitAsync($"show :{file}");
            if (result.Success)
            {
                conflicts.Add(new ConflictInfo
                {
                    FilePath = file,
                    Complexity = Interfaces.ConflictComplexity.Simple,
                    ResolutionSuggestion = "Manual resolution required"
                });
            }
        }

        return conflicts;
    }
}

/// <summary>
/// Configuration for repository settings.
/// </summary>
public class RepositorySettings
{
    public string? RepositoryPath { get; set; }
    public int GitTimeout { get; set; } = 30000;
    public bool LogOperations { get; set; } = true;
}
