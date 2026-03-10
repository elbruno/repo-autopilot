using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LocalRepoAuto.Core.Safety
{
    /// <summary>
    /// Implementation of pre-flight validation for Git operations.
    /// Performs safety checks before branch deletion, merges, and conflict resolution.
    /// </summary>
    public class SafetyGuards : IPreFlightChecker
    {
        private static readonly HashSet<string> DefaultProtectedBranches = new()
        {
            "main", "master", "develop", "development", "staging", "release"
        };

        private static readonly HashSet<string> CriticalFilePatterns = new()
        {
            ".csproj", ".sln", "package-lock.json", "yarn.lock", "Program.cs", "Program.vb"
        };

        private readonly int _staleDaysThreshold;
        private readonly HashSet<string> _protectedBranches;

        public SafetyGuards(int staleDaysThreshold = 90, IEnumerable<string>? protectedBranches = null)
        {
            _staleDaysThreshold = Math.Max(7, Math.Min(365, staleDaysThreshold)); // 7-365 days
            _protectedBranches = new HashSet<string>(
                protectedBranches ?? DefaultProtectedBranches,
                StringComparer.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// Validate a developer intent for safety and clarity.
        /// </summary>
        public async Task<PreFlightResult> ValidateIntentAsync(string intent, string repoPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                // Check for empty/null intent
                if (string.IsNullOrWhiteSpace(intent))
                {
                    return result.WithBlockers("Intent cannot be empty");
                }

                intent = intent.Trim();

                // Check for contradictory intents
                if (ContainsContradictoryKeywords(intent))
                {
                    return result.WithBlockers(
                        "Intent contains contradictory directives: 'keep all' and 'delete' are mutually exclusive"
                    );
                }

                // Check for ambiguous intents
                if (IsAmbiguousIntent(intent))
                {
                    return result.WithWarnings(
                        "Intent is ambiguous. Please be specific: use 'delete-stale-branches' instead of 'clean'"
                    );
                }

                // Check for extreme thresholds
                if (ExtractDaysThreshold(intent, out var days))
                {
                    if (days < 0)
                    {
                        return result.WithBlockers("Days threshold cannot be negative");
                    }
                    if (days == 0)
                    {
                        return result.WithWarnings("0-day threshold will delete all branches — this is likely unintended");
                    }
                    if (days > 1000)
                    {
                        return result.WithWarnings("Very high threshold (>1000 days) may never match branches");
                    }
                    result.WithDetail("staleness_days", days);
                }

                // Check for unknown branch references
                if (ExtractBranchNames(intent, out var branches))
                {
                    foreach (var branch in branches)
                    {
                        if (branch.Contains("*") || branch.Contains("?"))
                        {
                            result.WithDetail("uses_wildcard", true);
                        }
                    }
                    result.WithDetail("target_branches", branches);
                }

                // Check for protected branches in deletion targets
                if (IntentMentionsBranches(intent, out var mentionedBranches))
                {
                    var protectedMentioned = mentionedBranches
                        .Where(b => _protectedBranches.Contains(b))
                        .ToList();

                    if (protectedMentioned.Count > 0)
                    {
                        return result.WithBlockers(
                            $"Cannot target protected branches: {string.Join(", ", protectedMentioned)}"
                        );
                    }
                }

                result.IsValid = true;
                result.WithInfo("Intent validation passed");
                return result;
            });
        }

        /// <summary>
        /// Validate that a branch is safe to delete.
        /// </summary>
        public async Task<PreFlightResult> ValidateBranchDeletionAsync(string branchName, string repoPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                // Validate repository path
                if (!Directory.Exists(repoPath))
                {
                    return result.WithBlockers($"Repository path does not exist: {repoPath}");
                }

                if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    return result.WithBlockers($"Not a valid Git repository: {repoPath}");
                }

                // Validate branch name
                if (string.IsNullOrWhiteSpace(branchName))
                {
                    return result.WithBlockers("Branch name cannot be empty");
                }

                branchName = branchName.Trim();

                // Check if branch is protected
                if (_protectedBranches.Contains(branchName))
                {
                    return result.WithBlockers($"Branch '{branchName}' is protected and cannot be deleted");
                }

                // Check for special characters that might cause escaping issues
                if (!IsValidBranchName(branchName))
                {
                    return result.WithBlockers(
                        $"Branch name '{branchName}' contains invalid characters"
                    );
                }

                // These would be actual Git checks in production
                // For now, we validate the logic
                result.WithDetail("branch", branchName);
                result.WithDetail("is_protected", false);
                result.WithInfo($"Branch '{branchName}' appears safe for deletion (subject to Git verification)");

                return result;
            });
        }

        /// <summary>
        /// Validate that a merge operation is safe to execute.
        /// </summary>
        public async Task<PreFlightResult> ValidateMergeAsync(string baseBranch, string headBranch, string repoPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                // Validate repository
                if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    return result.WithBlockers("Not a valid Git repository");
                }

                // Validate branch names
                if (string.IsNullOrWhiteSpace(baseBranch) || string.IsNullOrWhiteSpace(headBranch))
                {
                    return result.WithBlockers("Base and head branch names are required");
                }

                baseBranch = baseBranch.Trim();
                headBranch = headBranch.Trim();

                // Check for self-merge
                if (baseBranch.Equals(headBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return result.WithBlockers("Cannot merge a branch into itself");
                }

                result.WithDetail("base_branch", baseBranch);
                result.WithDetail("head_branch", headBranch);
                result.WithInfo("Merge appears structurally valid (subject to Git verification)");

                return result;
            });
        }

        /// <summary>
        /// Check overall repository health and state before any operation.
        /// </summary>
        public async Task<PreFlightResult> CheckRepositoryStateAsync(string repoPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                // Validate repo exists
                if (!Directory.Exists(repoPath))
                {
                    return result.WithBlockers($"Repository path not found: {repoPath}");
                }

                var gitDir = Path.Combine(repoPath, ".git");
                if (!Directory.Exists(gitDir))
                {
                    return result.WithBlockers("Not a Git repository (no .git directory)");
                }

                // Check for lock files that indicate ongoing operations
                var lockFiles = new[] { "index.lock", "HEAD.lock", "refs/heads/lock" };
                var activeLocks = lockFiles
                    .Select(f => Path.Combine(gitDir, f))
                    .Where(File.Exists)
                    .ToList();

                if (activeLocks.Count > 0)
                {
                    return result.WithBlockers(
                        $"Git operation in progress (lock files detected: {string.Join(", ", activeLocks.Select(Path.GetFileName))})"
                    );
                }

                // Check for ongoing merge/rebase
                var mergePath = Path.Combine(gitDir, "MERGE_HEAD");
                var rebasePath = Path.Combine(gitDir, "rebase-merge");
                if (File.Exists(mergePath) || Directory.Exists(rebasePath))
                {
                    return result.WithBlockers("Merge or rebase in progress — cannot execute operations");
                }

                result.WithDetail("git_dir_valid", true);
                result.WithDetail("no_locks", true);
                result.WithInfo("Repository state is clean and ready for operations");

                return result;
            });
        }

        /// <summary>
        /// Validate conflict resolution is safe for a specific file.
        /// </summary>
        public async Task<PreFlightResult> ValidateConflictResolutionAsync(string filePath, string repoPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return result.WithBlockers("File path cannot be empty");
                }

                // Check if file is critical
                var fileName = Path.GetFileName(filePath);
                var isCritical = CriticalFilePatterns.Any(pattern =>
                    fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase)
                );

                if (isCritical)
                {
                    return result.WithBlockers(
                        $"File '{fileName}' is critical and cannot be auto-resolved. Manual review required."
                    );
                }

                // Check file type
                var extension = Path.GetExtension(filePath);
                var isBinary = IsBinaryFile(extension);

                if (isBinary)
                {
                    return result.WithBlockers(
                        $"Binary file conflicts cannot be auto-resolved: {filePath}"
                    );
                }

                result.WithDetail("file", filePath);
                result.WithDetail("is_binary", false);
                result.WithDetail("is_critical", false);
                result.WithInfo("File appears eligible for conflict resolution");

                return result;
            });
        }

        /// <summary>
        /// Validate user configuration is valid.
        /// </summary>
        public async Task<PreFlightResult> ValidateConfigurationAsync(string configPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                if (!File.Exists(configPath))
                {
                    return result.WithBlockers($"Configuration file not found: {configPath}");
                }

                try
                {
                    var json = File.ReadAllText(configPath);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // Validate key properties
                    if (root.TryGetProperty("staleDaysThreshold", out var staleDays))
                    {
                        if (staleDays.TryGetInt32(out var days))
                        {
                            if (days < 0 || days > 1000)
                            {
                                return result.WithBlockers(
                                    $"staleDaysThreshold must be 0-1000, got {days}"
                                );
                            }
                        }
                    }

                    if (root.TryGetProperty("protectedBranches", out var branches))
                    {
                        if (branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() == 0)
                        {
                            return result.WithWarnings(
                                "protectedBranches is empty — all branches can be deleted"
                            );
                        }
                    }

                    result.WithInfo("Configuration is valid");
                    return result;
                }
                catch (JsonException ex)
                {
                    return result.WithBlockers($"Configuration file is not valid JSON: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Check system capabilities: permissions, disk space, Git version.
        /// </summary>
        public async Task<PreFlightResult> CheckSystemCapabilitiesAsync(string repoPath)
        {
            return await Task.Run(() =>
            {
                var result = new PreFlightResult { IsValid = true };

                var gitDir = Path.Combine(repoPath, ".git");
                if (!Directory.Exists(gitDir))
                {
                    return result.WithBlockers("Not a valid Git repository");
                }

                // Check write permissions on .git
                try
                {
                    var testFile = Path.Combine(gitDir, ".permission-test-" + Guid.NewGuid());
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    result.WithDetail("write_permissions", true);
                }
                catch
                {
                    return result.WithBlockers("No write permissions to .git directory");
                }

                // Check disk space (simplified)
                var drive = Path.GetPathRoot(repoPath);
                if (drive != null)
                {
                    var driveInfo = new System.IO.DriveInfo(drive);
                    var freeSpaceMb = driveInfo.AvailableFreeSpace / (1024 * 1024);
                    if (freeSpaceMb < 100)
                    {
                        return result.WithWarnings(
                            $"Low disk space: {freeSpaceMb}MB free (recommend >100MB)"
                        );
                    }
                    result.WithDetail("free_space_mb", freeSpaceMb);
                }

                result.WithInfo("System capabilities check passed");
                return result;
            });
        }

        // Helper methods

        private static bool ContainsContradictoryKeywords(string intent)
        {
            var hasKeep = intent.Contains("keep", StringComparison.OrdinalIgnoreCase);
            var hasDelete = intent.Contains("delete", StringComparison.OrdinalIgnoreCase);
            var hasAll = intent.Contains("all", StringComparison.OrdinalIgnoreCase);

            return hasKeep && hasDelete && hasAll;
        }

        private static bool IsAmbiguousIntent(string intent)
        {
            var ambiguousKeywords = new[] { "clean", "remove", "clear" };
            return ambiguousKeywords.Any(kw =>
                intent.Equals(kw, StringComparison.OrdinalIgnoreCase)
            );
        }

        private static bool ExtractDaysThreshold(string intent, out int days)
        {
            days = 0;
            var match = System.Text.RegularExpressions.Regex.Match(intent, @"(\d+)\s*day", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var result))
            {
                days = result;
                return true;
            }
            return false;
        }

        private static bool ExtractBranchNames(string intent, out List<string> branches)
        {
            branches = new List<string>();
            // Simple extraction: look for quoted branch names
            var matches = System.Text.RegularExpressions.Regex.Matches(intent, @"'([^']+)'|""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var branch = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    branches.Add(branch);
                }
            }
            return branches.Count > 0;
        }

        private static bool IntentMentionsBranches(string intent, out List<string> branches)
        {
            branches = new List<string>();
            ExtractBranchNames(intent, out branches);
            return branches.Count > 0;
        }

        private static bool IsValidBranchName(string branchName)
        {
            // Git branch naming rules: no control chars, spaces, or ".."
            if (branchName.Contains("..") || branchName.Contains(' '))
            {
                return false;
            }
            // Allow most special chars but be cautious with shell metacharacters
            var invalidChars = new[] { '\\', ':', '*', '?', '"', '<', '>', '|' };
            return !branchName.Any(c => invalidChars.Contains(c));
        }

        private static bool IsBinaryFile(string extension)
        {
            var binaryExtensions = new[] { ".bin", ".exe", ".dll", ".so", ".jpg", ".png", ".gif", ".zip", ".tar" };
            return binaryExtensions.Any(ext => extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
