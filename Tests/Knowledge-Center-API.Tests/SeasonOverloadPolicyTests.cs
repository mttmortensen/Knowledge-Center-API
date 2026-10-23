using Knowledge_Center_API.Models.Seasons;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Tests
{
    public class SeasonOverloadPolicyTests
    {
        private static List<string> Names(int count) =>
            Enumerable.Range(1, count).Select(i => $"Interest {i}").ToList();

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(5, 3)]
        [InlineData(6, 4)]
        [InlineData(9, 4)]
        public void LevelFor_RisesWithEachFocusPastTwo(int focusCount, int expectedLevel)
        {
            Assert.Equal(expectedLevel, SeasonOverloadPolicy.LevelFor(focusCount));
        }

        [Fact]
        public void BuildWarning_IsNullWithinCapacity()
        {
            Assert.Null(SeasonOverloadPolicy.BuildWarning(Names(2)));
        }

        [Fact]
        public void BuildWarning_AsksForMoreAtEachLevel()
        {
            var l1 = SeasonOverloadPolicy.BuildWarning(Names(3))!.Requires;
            var l2 = SeasonOverloadPolicy.BuildWarning(Names(4))!.Requires;
            var l3 = SeasonOverloadPolicy.BuildWarning(Names(5))!.Requires;
            var l4 = SeasonOverloadPolicy.BuildWarning(Names(6))!.Requires;

            Assert.Null(l1.Phrase);
            Assert.Equal(0, l1.ReasonMinLength);

            Assert.NotNull(l2.Phrase);
            Assert.Equal(0, l2.ReasonMinLength);

            Assert.True(l3.Phrase!.Length > l2.Phrase!.Length);
            Assert.True(l3.ReasonMinLength > 0);
            Assert.Equal(0, l3.WaitSeconds);

            Assert.True(l4.Phrase!.Length > l3.Phrase.Length);
            Assert.True(l4.ReasonMinLength > l3.ReasonMinLength);
            Assert.True(l4.WaitSeconds > 0);
            Assert.Equal(l4.WaitSeconds, l4.WaitRemainingSeconds);
        }

        [Fact]
        public void BuildWarning_HighestLevelPhraseNamesEveryFocusInOrder()
        {
            var warning = SeasonOverloadPolicy.BuildWarning(new[] { "Rust", "lockpicking", "Chess", "Guitar", "Running", "Baking" })!;

            Assert.Equal(new[] { "Baking", "Chess", "Guitar", "lockpicking", "Running", "Rust" }, warning.FocusDomains);
            Assert.Contains("Baking, Chess, Guitar, lockpicking, Running and Rust", warning.Requires.Phrase);
            Assert.Contains("6", warning.Title);
        }

        [Fact]
        public void CheckAck_ForgivesCaseSpacingAndAFullStop()
        {
            var warning = SeasonOverloadPolicy.BuildWarning(Names(4))!;

            var ack = new SeasonOverloadAckDto { Phrase = "  i ACCEPT   slower progress on everything. " };

            Assert.Null(SeasonOverloadPolicy.CheckAck(warning, ack));
        }

        [Fact]
        public void CheckAck_RejectsAWrongPhrase()
        {
            var warning = SeasonOverloadPolicy.BuildWarning(Names(4))!;

            Assert.NotNull(SeasonOverloadPolicy.CheckAck(warning, new SeasonOverloadAckDto { Phrase = "ok" }));
            Assert.NotNull(SeasonOverloadPolicy.CheckAck(warning, new SeasonOverloadAckDto()));
        }

        [Fact]
        public void CheckAck_RequiresAReasonOfTheMinimumLength()
        {
            var warning = SeasonOverloadPolicy.BuildWarning(Names(5))!;
            string phrase = warning.Requires.Phrase!;

            var tooShort = new SeasonOverloadAckDto { Phrase = phrase, Reason = "because" };
            var padded = new SeasonOverloadAckDto { Phrase = phrase, Reason = "   " + new string('x', warning.Requires.ReasonMinLength - 1) + "   " };
            var enough = new SeasonOverloadAckDto { Phrase = phrase, Reason = new string('x', warning.Requires.ReasonMinLength) };

            Assert.NotNull(SeasonOverloadPolicy.CheckAck(warning, tooShort));
            Assert.NotNull(SeasonOverloadPolicy.CheckAck(warning, padded));
            Assert.Null(SeasonOverloadPolicy.CheckAck(warning, enough));
        }

        [Fact]
        public void CheckAck_FirstLevelNeedsNothingButTheToken()
        {
            var warning = SeasonOverloadPolicy.BuildWarning(Names(3))!;

            Assert.Null(SeasonOverloadPolicy.CheckAck(warning, new SeasonOverloadAckDto()));
        }
    }

    public class SeasonOverloadTokenTests
    {
        private static readonly DateTime Issued = new(2026, 10, 23, 18, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Validate_AcceptsTheSameLineupInAnyOrder()
        {
            string token = SeasonOverloadToken.Issue(7, new[] { 3, 1, 2 }, 1, Issued);

            var check = SeasonOverloadToken.Validate(token, 7, new[] { 2, 3, 1 }, 1, 0, Issued.AddSeconds(5));

            Assert.Equal(SeasonOverloadToken.Status.Valid, check.Status);
        }

        [Fact]
        public void Validate_RejectsADifferentSeasonLineupOrLevel()
        {
            string token = SeasonOverloadToken.Issue(7, new[] { 1, 2, 3 }, 1, Issued);
            var later = Issued.AddSeconds(5);

            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate(token, 8, new[] { 1, 2, 3 }, 1, 0, later).Status);
            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate(token, 7, new[] { 1, 2, 3, 4 }, 1, 0, later).Status);
            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate(token, 7, new[] { 1, 2, 4 }, 1, 0, later).Status);
            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate(token, 7, new[] { 1, 2, 3 }, 2, 0, later).Status);
        }

        [Fact]
        public void Validate_RejectsTamperedOrGarbageTokens()
        {
            string token = SeasonOverloadToken.Issue(7, new[] { 1, 2, 3 }, 1, Issued);
            string forged = SeasonOverloadToken.Issue(7, new[] { 1, 2, 3, 4 }, 2, Issued).Split('.')[0] + "." + token.Split('.')[1];
            var later = Issued.AddSeconds(5);

            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate(forged, 7, new[] { 1, 2, 3, 4 }, 2, 0, later).Status);
            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate("not-a-token", 7, new[] { 1, 2, 3 }, 1, 0, later).Status);
            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate("@@@.###", 7, new[] { 1, 2, 3 }, 1, 0, later).Status);
            Assert.Equal(SeasonOverloadToken.Status.Invalid, SeasonOverloadToken.Validate(null, 7, new[] { 1, 2, 3 }, 1, 0, later).Status);
        }

        [Fact]
        public void Validate_EnforcesTheWaitThenAccepts()
        {
            string token = SeasonOverloadToken.Issue(7, new[] { 1, 2, 3, 4, 5, 6 }, 4, Issued);
            int[] lineup = { 1, 2, 3, 4, 5, 6 };

            var early = SeasonOverloadToken.Validate(token, 7, lineup, 4, 60, Issued.AddSeconds(20));
            var onTime = SeasonOverloadToken.Validate(token, 7, lineup, 4, 60, Issued.AddSeconds(60));

            Assert.Equal(SeasonOverloadToken.Status.TooEarly, early.Status);
            Assert.Equal(40, early.WaitRemainingSeconds);
            Assert.Equal(SeasonOverloadToken.Status.Valid, onTime.Status);
            Assert.Equal(Issued, onTime.IssuedAtUtc);
        }

        [Fact]
        public void Validate_RejectsExpiredTokens()
        {
            string token = SeasonOverloadToken.Issue(7, new[] { 1, 2, 3 }, 1, Issued);

            var check = SeasonOverloadToken.Validate(token, 7, new[] { 1, 2, 3 }, 1, 0, Issued + SeasonOverloadToken.Lifetime + TimeSpan.FromSeconds(1));

            Assert.Equal(SeasonOverloadToken.Status.Invalid, check.Status);
        }
    }

    public class SeasonStatsBuilderTests
    {
        private static readonly DateTime From = new(2026, 9, 1);
        private static readonly DateTime To = new(2026, 10, 1);

        private static readonly List<SeasonDomain> Members = new()
        {
            new() { DomainId = 1, DomainName = "Rust", Role = "Focus" },
            new() { DomainId = 2, DomainName = "Lockpicking", Role = "Focus" },
            new() { DomainId = 3, DomainName = "Cybersecurity", Role = "Passive" }
        };

        [Fact]
        public void Build_GroupsByRoleAndListsIdleSeasonDomains()
        {
            var nodes = new[]
            {
                new SeasonStatsBuilder.NodeActivity(10, "Ownership", 1, "Rust", Logs: 4, ActionsCreated: 2, ActionsCompleted: 1, LearnEntries: 3),
                new SeasonStatsBuilder.NodeActivity(30, "Threat models", 3, "Cybersecurity", 2, 0, 0, 0),
                new SeasonStatsBuilder.NodeActivity(40, "Sourdough", 4, "Baking", 2, 0, 0, 0)
            };

            var stats = SeasonStatsBuilder.Build(From, To, nodes, Members, activeDays: 9, learnSessions: 1, unfiledLearnEntries: 2);

            Assert.Equal(new[] { "Rust", "Lockpicking", "Cybersecurity", "Baking" }, stats.ByDomain.Select(d => d.DomainName));
            Assert.Equal(new[] { "Focus", "Focus", "Passive", "Outside" }, stats.ByDomain.Select(d => d.Role));
            Assert.Equal(0, stats.ByDomain.Single(d => d.DomainId == 2).Score);
            Assert.Equal(8, stats.ByDomain.Single(d => d.DomainId == 1).Score);

            Assert.Equal(8, stats.Totals.Logs);
            Assert.Equal(2, stats.Totals.ActionsCreated);
            Assert.Equal(1, stats.Totals.ActionsCompleted);
            Assert.Equal(5, stats.Totals.LearnEntries); // 3 filed + 2 unfiled
            Assert.Equal(2, stats.UnfiledLearnEntries);
            Assert.Equal(9, stats.ActiveDays);
        }

        [Fact]
        public void Build_FocusShareIsTheFocusPartOfTheScore()
        {
            var nodes = new[]
            {
                new SeasonStatsBuilder.NodeActivity(10, "Ownership", 1, "Rust", 6, 5, 0, 0),
                new SeasonStatsBuilder.NodeActivity(40, "Sourdough", 4, "Baking", 2, 0, 0, 0)
            };

            var stats = SeasonStatsBuilder.Build(From, To, nodes, Members, 0, 0, 0);

            // Created actions aren't scored: 6 of 8.
            Assert.Equal(75.0, stats.FocusShare);
        }

        [Fact]
        public void Build_FocusShareIsZeroWithNoActivity()
        {
            var stats = SeasonStatsBuilder.Build(From, To, Array.Empty<SeasonStatsBuilder.NodeActivity>(), Members, 0, 0, 0);

            Assert.Equal(0, stats.FocusShare);
            Assert.Empty(stats.TopKnowledgeNodes);
            Assert.Equal(3, stats.ByDomain.Count);
        }

        [Fact]
        public void Build_TopNodesAreTheHighestScoringFive()
        {
            var nodes = Enumerable.Range(1, 8)
                .Select(i => new SeasonStatsBuilder.NodeActivity(i, $"Node {i}", 1, "Rust", i, 0, 0, 0))
                .Append(new SeasonStatsBuilder.NodeActivity(99, "Only planned", 1, "Rust", 0, 10, 0, 0))
                .ToList();

            var stats = SeasonStatsBuilder.Build(From, To, nodes, Members, 0, 0, 0);

            Assert.Equal(new[] { 8, 7, 6, 5, 4 }, stats.TopKnowledgeNodes.Select(n => n.NodeId));
            Assert.All(stats.TopKnowledgeNodes, n => Assert.Equal("Focus", n.Role));
        }
    }
}
