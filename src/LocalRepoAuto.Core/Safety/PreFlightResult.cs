using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalRepoAuto.Core.Safety
{
    /// <summary>
    /// Result of pre-flight validation for a Git operation.
    /// Provides detailed information about what passed, what failed, and what warnings were raised.
    /// </summary>
    public class PreFlightResult
    {
        /// <summary>Whether the operation is safe to proceed.</summary>
        public bool IsValid { get; set; }

        /// <summary>Blocking issues that prevent operation (must be resolved).</summary>
        public IReadOnlyList<string> Blockers { get; private set; } = new List<string>();

        /// <summary>Warnings that should be reviewed but don't block operation.</summary>
        public IReadOnlyList<string> Warnings { get; private set; } = new List<string>();

        /// <summary>Information messages for audit/logging.</summary>
        public IReadOnlyList<string> InfoMessages { get; private set; } = new List<string>();

        /// <summary>Approvals required from user before proceeding (e.g., "delete-protected-branch").</summary>
        public IReadOnlyList<string> RequiredApprovals { get; private set; } = new List<string>();

        /// <summary>Operation-specific details (merge conflicts, branch info, etc.).</summary>
        public Dictionary<string, object> Details { get; private set; } = new();

        /// <summary>Timestamp when validation was performed.</summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Create a successful result (all checks passed).</summary>
        public static PreFlightResult Success(string? infoMessage = null)
        {
            var result = new PreFlightResult { IsValid = true };
            if (infoMessage != null)
            {
                result.InfoMessages = new List<string> { infoMessage };
            }
            return result;
        }

        /// <summary>Create a failure result with blockers.</summary>
        public static PreFlightResult Failure(params string[] blockers)
        {
            return new PreFlightResult
            {
                IsValid = false,
                Blockers = blockers.ToList()
            };
        }

        /// <summary>Fluent API: add blockers.</summary>
        public PreFlightResult WithBlockers(params string[] blockers)
        {
            IsValid = false;
            var list = new List<string>(Blockers);
            list.AddRange(blockers);
            Blockers = list;
            return this;
        }

        /// <summary>Fluent API: add warnings.</summary>
        public PreFlightResult WithWarnings(params string[] warnings)
        {
            var list = new List<string>(Warnings);
            list.AddRange(warnings);
            Warnings = list;
            return this;
        }

        /// <summary>Fluent API: add info messages.</summary>
        public PreFlightResult WithInfo(params string[] messages)
        {
            var list = new List<string>(InfoMessages);
            list.AddRange(messages);
            InfoMessages = list;
            return this;
        }

        /// <summary>Fluent API: require approval for operation.</summary>
        public PreFlightResult RequireApproval(params string[] approvalTypes)
        {
            var list = new List<string>(RequiredApprovals);
            list.AddRange(approvalTypes);
            RequiredApprovals = list;
            return this;
        }

        /// <summary>Fluent API: add detail information.</summary>
        public PreFlightResult WithDetail(string key, object? value)
        {
            Details[key] = value ?? "null";
            return this;
        }

        /// <summary>Get human-readable summary of validation result.</summary>
        public string GetSummary()
        {
            if (IsValid && Blockers.Count == 0)
            {
                return "✓ Pre-flight checks passed. Operation is safe to proceed.";
            }

            var summary = "✗ Pre-flight validation failed:\n";
            
            if (Blockers.Count > 0)
            {
                summary += $"  Blockers ({Blockers.Count}):\n";
                foreach (var blocker in Blockers)
                {
                    summary += $"    - {blocker}\n";
                }
            }

            if (Warnings.Count > 0)
            {
                summary += $"  Warnings ({Warnings.Count}):\n";
                foreach (var warning in Warnings)
                {
                    summary += $"    - {warning}\n";
                }
            }

            if (RequiredApprovals.Count > 0)
            {
                summary += $"  Required Approvals: {string.Join(", ", RequiredApprovals)}\n";
            }

            return summary;
        }
    }
}
