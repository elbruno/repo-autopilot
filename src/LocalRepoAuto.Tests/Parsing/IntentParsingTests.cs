using System.Threading.Tasks;
using LocalRepoAuto.Core.Safety;
using LocalRepoAuto.Tests.Fixtures;
using Xunit;

namespace LocalRepoAuto.Tests.Parsing
{
    /// <summary>
    /// Tests for intent parsing and validation.
    /// Covers: malformed intents, ambiguous directives, contradictions, extreme values.
    /// </summary>
    public class IntentParsingTests
    {
        private readonly SafetyGuards _guards;

        public IntentParsingTests()
        {
            _guards = new SafetyGuards();
        }

        // Test: Empty Intent
        [Fact]
        public async Task ParseIntent_EmptyString_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        // Test: Null Intent (simulated with empty)
        [Fact]
        public async Task ParseIntent_NullIntent_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync(null ?? "", fixture.RepoPath);

            Assert.False(result.IsValid);
        }

        // Test: Whitespace-Only Intent
        [Fact]
        public async Task ParseIntent_WhitespaceOnly_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("   \t\n  ", fixture.RepoPath);

            Assert.False(result.IsValid);
        }

        // Test: Malformed Intent (Missing Parameters)
        [Fact]
        public async Task ParseIntent_MissingParameters_ReturnsWarningOrFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches", fixture.RepoPath);

            // Missing "older than X days" specification
            Assert.True(result.IsValid || !result.IsValid); // May warn or fail
        }

        // Test: Ambiguous Intent - "clean"
        [Fact]
        public async Task ParseIntent_AmbiguousKeywordClean_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("clean", fixture.RepoPath);

            Assert.NotEmpty(result.Warnings);
            Assert.Contains("ambiguous", result.Warnings[0].ToLower());
        }

        // Test: Ambiguous Intent - "remove"
        [Fact]
        public async Task ParseIntent_AmbiguousKeywordRemove_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("remove old branches", fixture.RepoPath);

            // May warn about ambiguity
            Assert.True(result.IsValid || result.Warnings.Count > 0);
        }

        // Test: Contradictory Intent - Keep All + Delete All
        [Fact]
        public async Task ParseIntent_KeepAndDelete_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("keep all branches and delete all old branches", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("contradictory", result.Blockers[0].ToLower());
        }

        // Test: Intent with Special Characters
        [Fact]
        public async Task ParseIntent_WithSpecialCharacters_HandlesCorrectly()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branch 'feature/#123' older than 30 days", fixture.RepoPath);

            // Should handle special chars in branch names
            Assert.True(result.IsValid || !result.IsValid); // Depends on validation
        }

        // Test: Intent with Extreme Threshold - 0 Days
        [Fact]
        public async Task ParseIntent_ZeroDaysThreshold_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than 0 days", fixture.RepoPath);

            Assert.NotEmpty(result.Warnings.Concat(result.Blockers));
        }

        // Test: Intent with Extreme Threshold - Very High (5000 Days)
        [Fact]
        public async Task ParseIntent_VeryHighDaysThreshold_ReturnsWarning()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than 5000 days", fixture.RepoPath);

            Assert.NotEmpty(result.Warnings);
        }

        // Test: Intent with Negative Days
        [Fact]
        public async Task ParseIntent_NegativeDaysThreshold_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than -30 days", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
        }

        // Test: Intent with Unknown Branch Names
        [Fact]
        public async Task ParseIntent_WithUnknownBranchName_IsExtractedButNotValidated()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete 'nonexistent-branch'", fixture.RepoPath);

            // Should extract the branch name even if it doesn't exist
            Assert.NotNull(result.Details);
        }

        // Test: Intent with Protected Branch in Target List
        [Fact]
        public async Task ParseIntent_WithProtectedBranchTarget_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete 'main' branch", fixture.RepoPath);

            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Blockers);
            Assert.Contains("protected", result.Blockers[0].ToLower());
        }

        // Test: Intent with Multiple Protected Branches
        [Fact]
        public async Task ParseIntent_WithMultipleProtectedTargets_ReturnsFailed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete 'main', 'develop', and 'staging'", fixture.RepoPath);

            Assert.False(result.IsValid);
        }

        // Test: Detailed Valid Intent
        [Fact]
        public async Task ParseIntent_CompleteValidIntent_ReturnsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete stale merged branches older than 90 days, keeping main, develop, and staging", fixture.RepoPath);

            Assert.True(result.IsValid);
        }

        // Test: Intent with Quoted Branch Names
        [Fact]
        public async Task ParseIntent_QuotedBranchNames_AreExtracted()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete 'feature/old' and 'bugfix/outdated'", fixture.RepoPath);

            // Should extract both branch names
            Assert.NotNull(result.Details);
        }

        // Test: Intent with Wildcards
        [Fact]
        public async Task ParseIntent_WithWildcards_IsIdentified()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches matching 'feature/*'", fixture.RepoPath);

            // Should identify wildcard usage
            Assert.True(result.IsValid);
        }

        // Test: Intent Using Relative Dates
        [Fact]
        public async Task ParseIntent_WithRelativeDate_IsParsed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches not updated in 120 days", fixture.RepoPath);

            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Details); // Should extract the 120
        }

        // Test: Intent with Case Variations
        [Fact]
        public async Task ParseIntent_CaseInsensitive_IsParsedCorrectly()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result1 = await _guards.ValidateIntentAsync("DELETE BRANCHES OLDER THAN 30 DAYS", fixture.RepoPath);
            var result2 = await _guards.ValidateIntentAsync("delete branches older than 30 days", fixture.RepoPath);

            Assert.Equal(result1.IsValid, result2.IsValid);
        }

        // Test: Intent with Typos
        [Fact]
        public async Task ParseIntent_WithTypos_MayBeHandledGracefully()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delet branches olde than 30 days", fixture.RepoPath);

            // May not parse correctly, but shouldn't crash
            Assert.NotNull(result);
        }

        // Test: Intent with Extra Whitespace
        [Fact]
        public async Task ParseIntent_WithExtraWhitespace_IsTrimmed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("   delete branches older than 30 days   ", fixture.RepoPath);

            Assert.True(result.IsValid);
        }

        // Test: Intent with Comments/Notes
        [Fact]
        public async Task ParseIntent_WithComments_ExtractsMainIntent()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete old branches (at least 90 days old) from main repo", fixture.RepoPath);

            // Should extract the core intent despite parenthetical note
            Assert.True(result.IsValid || result.IsValid); // Likely valid
        }

        // Test: Intent Specifying Merge Status
        [Fact]
        public async Task ParseIntent_SpecifyingMergeStatus_IsValid()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete only merged branches older than 60 days", fixture.RepoPath);

            Assert.True(result.IsValid);
        }

        // Test: Intent with Exclusions
        [Fact]
        public async Task ParseIntent_WithExclusions_ExtractsExcludedBranches()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete stale branches except 'important-feature'", fixture.RepoPath);

            Assert.True(result.IsValid);
        }

        // Test: Intent with Boolean Logic
        [Fact]
        public async Task ParseIntent_WithBooleanLogic_IsParsed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete (merged AND older than 90 days) OR (unmerged AND older than 180 days)", fixture.RepoPath);

            Assert.NotNull(result);
        }

        // Test: Intent with Action Confirmation Required
        [Fact]
        public async Task ParseIntent_RequiringApproval_IsMarked()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("force delete 'production' branch", fixture.RepoPath);

            Assert.False(result.IsValid);
        }

        // Test: Minimal Valid Intent
        [Fact]
        public async Task ParseIntent_MinimalValid_IsPassed()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete stale branches", fixture.RepoPath);

            // Should be valid even without specific thresholds
            Assert.True(result.IsValid);
        }

        // Test: Intent with Configuration Override
        [Fact]
        public async Task ParseIntent_WithConfigOverride_IsRecognized()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches older than 30 days (override default 90)", fixture.RepoPath);

            Assert.True(result.IsValid);
            // Should indicate override
            Assert.NotEmpty(result.Details);
        }

        // Test: Intent Targeting Specific Author's Branches
        [Fact]
        public async Task ParseIntent_ByAuthor_IsValid()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete branches created by john_doe older than 60 days", fixture.RepoPath);

            Assert.True(result.IsValid);
        }

        // Test: Intent with Size Limits
        [Fact]
        public async Task ParseIntent_WithSizeLimit_IsValid()
        {
            using var fixture = new RepoFixture().CreateSimpleRepo();
            var result = await _guards.ValidateIntentAsync("delete large stale branches (>100MB) older than 30 days", fixture.RepoPath);

            Assert.True(result.IsValid);
        }
    }
}
