using System.Threading.Tasks;
using LocalRepoAuto.Core.Safety;
using LocalRepoAuto.Tests.Fixtures;
using Xunit;

namespace LocalRepoAuto.Tests.Safety
{
    /// <summary>
    /// Tests for SafetyGuards pre-flight validation.
    /// Covers: intent validation, branch deletion checks, merge validation, state checks.
    /// </summary>
    public class SafetyGuardsTests
    {
        private readonly SafetyGuards _guards;

        public SafetyGuardsTests()
        {
            _guards = new SafetyGuards(staleDaysThreshold: 90);
        }

        // ============== Intent Validation Tests ==============

        [Fact]
        public async Task ValidateIntent_WithEmptyIntent_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("empty", result.Blockers[0].ToLower());
        }

        [Fact]
        public async Task ValidateIntent_WithValidDeleteStaleIntent_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than 30 days", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Blockers);
        }

        [Fact]
        public async Task ValidateIntent_WithContradictoryKeywords_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("keep all branches and delete all old branches", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("contradictory", result.Blockers[0].ToLower());
        }

        [Fact]
        public async Task ValidateIntent_WithAmbiguousKeyword_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("clean", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains("ambiguous", result.Warnings[0].ToLower());
        }

        [Fact]
        public async Task ValidateIntent_WithExtremeLowThreshold_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than 0 days", fixture.RepoPath);

            Assert.Contains(result.Warnings.Concat(result.Blockers), w => w.ToLower().Contains("0-day"));
        }

        [Fact]
        public async Task ValidateIntent_WithExtremeHighThreshold_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than 5000 days", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact]
        public async Task ValidateIntent_WithProtectedBranchTarget_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete 'main' branch", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("protected", result.Blockers[0].ToLower());
        }

        // ============== Branch Deletion Validation Tests ==============

        [Fact]
        public async Task ValidateBranchDeletion_WithEmptyBranchName_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateBranchDeletionAsync("", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        [Fact]
        public async Task ValidateBranchDeletion_WithProtectedBranch_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateBranchDeletionAsync("main", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("protected", result.Blockers[0].ToLower());
        }

        [Fact]
        public async Task ValidateBranchDeletion_WithValidBranchName_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            fixture.CreateBranch("feature-x");
            var result = await _guards.ValidateBranchDeletionAsync("feature-x", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Blockers);
        }

        [Fact]
        public async Task ValidateBranchDeletion_WithInvalidRepoPath_ReturnsFailed()
        {
            var result = await _guards.ValidateBranchDeletionAsync("feature-x", "/nonexistent/path");

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        [Fact]
        public async Task ValidateBranchDeletion_WithInvalidCharacters_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateBranchDeletionAsync("feature:bad*name", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        // ============== Merge Validation Tests ==============

        [Fact]
        public async Task ValidateMerge_WithEmptyBaseBranch_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateMergeAsync("", "feature", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        [Fact]
        public async Task ValidateMerge_WithSameBranch_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateMergeAsync("main", "main", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("itself", result.Blockers[0].ToLower());
        }

        [Fact]
        public async Task ValidateMerge_WithValidBranches_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            fixture.CreateBranch("feature");
            var result = await _guards.ValidateMergeAsync("main", "feature", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Blockers);
        }

        // ============== Repository State Validation Tests ==============

        [Fact]
        public async Task CheckRepositoryState_WithNonGitDirectory_ReturnsFailed()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"not-a-repo-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var result = await _guards.CheckRepositoryStateAsync(tempDir);

                Assert.False(result.IsValid);
                Assert.NotEmpty(result.Blockers);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task CheckRepositoryState_WithCleanRepo_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.CheckRepositoryStateAsync(fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Blockers);
        }

        [Fact]
        public async Task CheckRepositoryState_WithNonexistentPath_ReturnsFailed()
        {
            var result = await _guards.CheckRepositoryStateAsync("/nonexistent/path");

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        // ============== Conflict Resolution Validation Tests ==============

        [Fact]
        public async Task ValidateConflictResolution_WithCriticalFile_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateConflictResolutionAsync("Program.cs", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("critical", result.Blockers[0].ToLower());
        }

        [Fact]
        public async Task ValidateConflictResolution_WithProjectFile_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateConflictResolutionAsync("App.csproj", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        [Fact]
        public async Task ValidateConflictResolution_WithRegularFile_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateConflictResolutionAsync("README.md", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Blockers);
        }

        [Fact]
        public async Task ValidateConflictResolution_WithBinaryFile_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateConflictResolutionAsync("image.png", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("binary", result.Blockers[0].ToLower());
        }

        // ============== Configuration Validation Tests ==============

        [Fact]
        public async Task ValidateConfiguration_WithNonexistentFile_ReturnsFailed()
        {
            var result = await _guards.ValidateConfigurationAsync("/nonexistent/config.json");

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        [Fact]
        public async Task ValidateConfiguration_WithValidJson_ReturnsPassed()
        {
            var configFile = Path.Combine(Path.GetTempPath(), $"config-{Guid.NewGuid()}.json");
            var json = @"{
                ""staleDaysThreshold"": 90,
                ""protectedBranches"": [""main"", ""develop""]
            }";
            File.WriteAllText(configFile, json);

            try
            {
                var result = await _guards.ValidateConfigurationAsync(configFile);

                Assert.True(result.IsValid);
                Assert.Empty(result.Blockers);
            }
            finally
            {
                File.Delete(configFile);
            }
        }

        [Fact]
        public async Task ValidateConfiguration_WithInvalidJson_ReturnsFailed()
        {
            var configFile = Path.Combine(Path.GetTempPath(), $"config-{Guid.NewGuid()}.json");
            File.WriteAllText(configFile, "{ invalid json }");

            try
            {
                var result = await _guards.ValidateConfigurationAsync(configFile);

                Assert.False(result.IsValid);
                Assert.NotEmpty(result.Blockers);
            }
            finally
            {
                File.Delete(configFile);
            }
        }

        [Fact]
        public async Task ValidateConfiguration_WithEmptyProtectedBranches_ReturnsWarning()
        {
            var configFile = Path.Combine(Path.GetTempPath(), $"config-{Guid.NewGuid()}.json");
            var json = @"{
                ""staleDaysThreshold"": 90,
                ""protectedBranches"": []
            }";
            File.WriteAllText(configFile, json);

            try
            {
                var result = await _guards.ValidateConfigurationAsync(configFile);

                Assert.True(result.IsValid);
                Assert.NotEmpty(result.Warnings);
            }
            finally
            {
                File.Delete(configFile);
            }
        }

        [Fact]
        public async Task ValidateConfiguration_WithInvalidThreshold_ReturnsFailed()
        {
            var configFile = Path.Combine(Path.GetTempPath(), $"config-{Guid.NewGuid()}.json");
            var json = @"{
                ""staleDaysThreshold"": 5000,
                ""protectedBranches"": [""main""]
            }";
            File.WriteAllText(configFile, json);

            try
            {
                var result = await _guards.ValidateConfigurationAsync(configFile);

                Assert.False(result.IsValid);
                Assert.NotEmpty(result.Blockers);
            }
            finally
            {
                File.Delete(configFile);
            }
        }

        // ============== System Capabilities Tests ==============

        [Fact]
        public async Task CheckSystemCapabilities_WithValidRepo_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.CheckSystemCapabilitiesAsync(fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Blockers);
        }

        [Fact]
        public async Task CheckSystemCapabilities_WithInvalidPath_ReturnsFailed()
        {
            var result = await _guards.CheckSystemCapabilitiesAsync("/nonexistent/path");

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        // ============== Result Builder Fluent API Tests ==============

        [Fact]
        public void PreFlightResult_FluentApi_BuildsCorrectly()
        {
            var result = PreFlightResult.Success("Operation approved")
                .WithWarnings("Proceeding with caution")
                .WithDetail("branch_count", 5);

            Assert.True(result.IsValid);
            Assert.Single(result.InfoMessages);
            Assert.Single(result.Warnings);
            Assert.Contains("branch_count", result.Details.Keys);
            Assert.Equal(5, result.Details["branch_count"]);
        }

        [Fact]
        public void PreFlightResult_Failure_BuildsCorrectly()
        {
            var result = PreFlightResult.Failure("Blocker 1", "Blocker 2")
                .WithWarnings("Warning 1");

            Assert.False(result.IsValid);
            Assert.Equal(2, result.Blockers.Count);
            Assert.Single(result.Warnings);
        }

        [Fact]
        public void PreFlightResult_GetSummary_FormatsCorrectly()
        {
            var result = PreFlightResult.Failure("Cannot delete protected branch")
                .WithWarnings("Working directory has uncommitted changes");

            var summary = result.GetSummary();

            Assert.Contains("Blockers", summary);
            Assert.Contains("Warnings", summary);
            Assert.Contains("protected", summary);
        }
    }
}
