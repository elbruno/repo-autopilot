using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LocalRepoAuto.Tests.Fixtures
{
    /// <summary>
    /// Creates and manages temporary Git repositories for testing.
    /// Provides fixtures for various test scenarios: simple repos, stale branches, conflicts, edge cases.
    /// </summary>
    public class RepoFixture : IDisposable
    {
        public string RepoPath { get; private set; }
        private bool _disposed = false;

        public RepoFixture(string? basePath = null)
        {
            var baseDir = basePath ?? Path.Combine(Path.GetTempPath(), "GitAgentTests");
            Directory.CreateDirectory(baseDir);
            RepoPath = Path.Combine(baseDir, $"test-repo-{Guid.NewGuid()}");
        }

        /// <summary>Create a simple repo with a main branch and 2 commits.</summary>
        public RepoFixture CreateSimpleRepo()
        {
            Directory.CreateDirectory(RepoPath);
            RunGit("init");
            ConfigureGit();

            CreateFile("README.md", "# Test Repository");
            RunGit("add .");
            RunGit("commit -m 'Initial commit'");

            CreateFile("file1.txt", "content1");
            RunGit("add .");
            RunGit("commit -m 'Add file1'");

            return this;
        }

        /// <summary>Create a repo with stale and fresh branches (mixed merge states).</summary>
        public RepoFixture CreateStaleBranchesRepo()
        {
            CreateSimpleRepo();

            // Create stale merged branch (100 days old)
            CreateBranch("stale-merged", daysOld: 100);
            CreateFile("stale-feature.txt", "old feature");
            RunGit("add .");
            RunGit("commit -m 'Stale feature'");
            RunGit("checkout main");
            RunGit("merge --no-ff stale-merged -m 'Merge stale-merged'");

            // Create stale unmerged branch (120 days old)
            CreateBranch("stale-unmerged", daysOld: 120);
            CreateFile("unmerged.txt", "unmerged work");
            RunGit("add .");
            RunGit("commit -m 'Unmerged work'");

            // Create recent branch (5 days old)
            RunGit("checkout main");
            CreateBranch("recent-feature", daysOld: 5);
            CreateFile("recent.txt", "recent work");
            RunGit("add .");
            RunGit("commit -m 'Recent work'");

            RunGit("checkout main");
            return this;
        }

        /// <summary>Create a repo with conflicting branches.</summary>
        public RepoFixture CreateConflictRepo()
        {
            CreateSimpleRepo();

            // Create feature branch with conflicting change
            RunGit("checkout -b feature");
            CreateFile("conflict.txt", "feature version");
            RunGit("add .");
            RunGit("commit -m 'Add conflict file (feature)'");

            // Make conflicting change on main
            RunGit("checkout main");
            CreateFile("conflict.txt", "main version");
            RunGit("add .");
            RunGit("commit -m 'Add conflict file (main)'");

            return this;
        }

        /// <summary>Create a repo with complex history (multiple branches, merges).</summary>
        public RepoFixture CreateComplexHistoryRepo()
        {
            CreateSimpleRepo();

            // Create and merge feature branches
            for (int i = 1; i <= 3; i++)
            {
                RunGit($"checkout -b feature-{i}");
                CreateFile($"feature{i}.txt", $"Feature {i}");
                RunGit("add .");
                RunGit($"commit -m 'Feature {i}'");
                RunGit("checkout main");
                RunGit($"merge --no-ff feature-{i} -m 'Merge feature-{i}'");
            }

            return this;
        }

        /// <summary>Create a repo with special characters in branch names.</summary>
        public RepoFixture CreateEdgeCaseRepo()
        {
            CreateSimpleRepo();

            // Create branches with special characters (safe ones)
            var branchNames = new[] { "feature/issue-123", "bugfix/JIRA-456", "release/v1.0.0" };
            foreach (var branch in branchNames)
            {
                RunGit($"checkout -b '{branch}'");
                CreateFile($"{branch.Replace('/', '-')}.txt", "content");
                RunGit("add .");
                RunGit($"commit -m 'Work on {branch}'");
                RunGit("checkout main");
            }

            return this;
        }

        /// <summary>Create a branch with a specific age (by manipulating commit dates).</summary>
        public void CreateBranch(string branchName, int daysOld = 0)
        {
            RunGit($"checkout -b {branchName}");
            CreateFile($"{branchName}.txt", $"Content for {branchName}");
            RunGit("add .");

            var date = DateTime.Now.AddDays(-daysOld);
            var dateStr = date.ToString("ddd MMM d HH:mm:ss yyyy zzz");
            RunGit($"commit -m 'Create {branchName}'", new Dictionary<string, string>
            {
                { "GIT_AUTHOR_DATE", dateStr },
                { "GIT_COMMITTER_DATE", dateStr }
            });

            RunGit("checkout main");
        }

        /// <summary>Merge a branch into the current branch.</summary>
        public void MergeBranch(string branchName, string targetBranch = "main")
        {
            RunGit($"checkout {targetBranch}");
            RunGit($"merge --no-ff {branchName} -m 'Merge {branchName}'");
        }

        /// <summary>Get list of branches in this repo.</summary>
        public List<string> GetBranches()
        {
            var output = RunGitAndCapture("branch --list");
            return output
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().TrimStart('*', ' '))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        /// <summary>Get the current branch.</summary>
        public string GetCurrentBranch()
        {
            var output = RunGitAndCapture("rev-parse --abbrev-ref HEAD");
            return output.Trim();
        }

        /// <summary>Get commit log as list of commit messages.</summary>
        public List<string> GetLog()
        {
            var output = RunGitAndCapture("log --oneline");
            return output
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        /// <summary>Get reflog entries (for recovery verification).</summary>
        public List<string> GetReflog()
        {
            var output = RunGitAndCapture("reflog");
            return output
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        /// <summary>Get git status output.</summary>
        public string GetStatus()
        {
            return RunGitAndCapture("status --porcelain");
        }

        /// <summary>Verify working directory is clean.</summary>
        public bool IsWorkingDirectoryClean()
        {
            return string.IsNullOrWhiteSpace(GetStatus());
        }

        /// <summary>Check if branch exists.</summary>
        public bool BranchExists(string branchName)
        {
            return GetBranches().Contains(branchName);
        }

        /// <summary>Checkout a specific branch.</summary>
        public void Checkout(string branchName)
        {
            RunGit($"checkout {branchName}");
        }

        /// <summary>Delete a branch locally.</summary>
        public void DeleteBranch(string branchName)
        {
            RunGit($"branch -d {branchName}");
        }

        /// <summary>Force delete a branch.</summary>
        public void ForceDeleteBranch(string branchName)
        {
            RunGit($"branch -D {branchName}");
        }

        /// <summary>Create a file with content in the repo.</summary>
        public void CreateFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RepoPath, relativePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(fullPath, content);
        }

        /// <summary>Configure Git user for this repo.</summary>
        private void ConfigureGit()
        {
            RunGit("config user.name 'Test User'");
            RunGit("config user.email 'test@example.com'");
        }

        /// <summary>Run a Git command in the repo.</summary>
        private void RunGit(string args, Dictionary<string, string>? envVars = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = RepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (envVars != null)
            {
                foreach (var (key, value) in envVars)
                {
                    psi.Environment[key] = value;
                }
            }

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start git process");

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Git command failed: {args}\n{error}");
            }
        }

        /// <summary>Run a Git command and capture output.</summary>
        private string RunGitAndCapture(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = RepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start git process");

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (Directory.Exists(RepoPath))
                {
                    // Mark files as not read-only so they can be deleted
                    var dirInfo = new DirectoryInfo(RepoPath);
                    foreach (var file in dirInfo.GetFiles("*", System.IO.SearchOption.AllDirectories))
                    {
                        file.Attributes = System.IO.FileAttributes.Normal;
                    }
                    Directory.Delete(RepoPath, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }

            _disposed = true;
        }
    }
}
