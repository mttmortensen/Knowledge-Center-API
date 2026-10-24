using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Knowledge_Center_API.Models.Seasons;
using Knowledge_Center_API.Services.Security;

namespace Knowledge_Center_API.Tests
{
    /*
     * Seasons. Only one season can be active in the whole database, so every
     * test starts by clearing the Seasons table, and seeds its own domains.
     */
    public class SeasonDbFixture : IDisposable
    {
        public TestApiFactory Factory { get; } = new();

        public SeasonDbFixture()
        {
            if (string.IsNullOrWhiteSpace(TestDatabase.ConnectionString)) return;
            TestDatabase.Reset();
        }

        private readonly string _token = AuthSession.CreateSession("season-tests");

        public HttpClient CreateClient()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return client;
        }

        public void Dispose() => Factory.Dispose();
    }

    [Collection(DatabaseCollection.Name)]
    public class SeasonEndpointTests : IClassFixture<SeasonDbFixture>
    {
        private readonly SeasonDbFixture _fx;
        private readonly HttpClient _client;

        public SeasonEndpointTests(SeasonDbFixture fx)
        {
            _fx = fx;
            _client = fx.CreateClient();
            if (!string.IsNullOrWhiteSpace(TestDatabase.ConnectionString))
                TestDatabase.Scalar(@"WITH ""Gone"" AS (DELETE FROM ""Seasons"" RETURNING 1) SELECT COUNT(*) FROM ""Gone"";");
        }

        /* ===================== HELPERS ===================== */

        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

        private static int SeedDomain(string name, bool archived = false) =>
            TestDatabase.Scalar($@"INSERT INTO ""Domains"" (""DomainName"", ""DomainStatus"", ""IsArchived"") VALUES ('{name}', 'Active', {archived}) RETURNING ""DomainId"";");

        private static int SeedNode(int domainId, string title) =>
            TestDatabase.Scalar($@"INSERT INTO ""KnowledgeNodes"" (""Title"", ""Description"", ""ConfidenceLevel"", ""Status"", ""CreatedAt"", ""LastUpdated"", ""DomainId"") VALUES ('{title}', 'Desc', 3, 'Learning', now(), now(), {domainId}) RETURNING ""Id"";");

        private static List<int> SeedDomains(int count) =>
            Enumerable.Range(1, count).Select(i => SeedDomain($"Interest {Guid.NewGuid():N}")).ToList();

        private async Task<SeasonDetailsDto> NewSeason(string name = "Season", int startOffset = 0, int lengthDays = 60)
        {
            var res = await _client.PostAsJsonAsync("/kc/api/seasons", new
            {
                Name = name,
                PlannedStart = Today.AddDays(startOffset),
                PlannedEnd = Today.AddDays(startOffset + lengthDays)
            });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return (await res.Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
        }

        private static object Lineup(IEnumerable<int> focus, IEnumerable<int>? passive = null, object? ack = null) => new
        {
            Domains = focus.Select(id => new { DomainId = id, Role = "Focus" })
                .Concat((passive ?? Enumerable.Empty<int>()).Select(id => new { DomainId = id, Role = "Passive" }))
                .ToList(),
            OverloadAck = ack
        };

        private Task<HttpResponseMessage> PutLineup(int seasonId, object lineup) =>
            _client.PutAsJsonAsync($"/kc/api/seasons/{seasonId}/domains", lineup);

        private static async Task<SeasonOverloadWarningDto> Warning(HttpResponseMessage res)
        {
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var warning = await res.Content.ReadFromJsonAsync<SeasonOverloadWarningDto>();
            Assert.Equal("FocusOverload", warning!.Warning);
            return warning;
        }

        private static int FocusCountInDb(int seasonId) =>
            TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""SeasonDomains"" WHERE ""SeasonId"" = {seasonId} AND ""Role"" = 'Focus';");

        /* ===================== SEASONS ===================== */

        [DbFact]
        public async Task Create_StartsPlannedWithDerivedDays()
        {
            var season = await NewSeason("Autumn", startOffset: 0, lengthDays: 56);

            Assert.Equal("Planned", season.Status);
            Assert.Equal(56, season.DaysPlanned);
            Assert.Null(season.DaysElapsed);
            Assert.Null(season.Stats);
            Assert.Empty(season.Domains);
        }

        [DbFact]
        public async Task Create_RejectsBadOrOverlappingDates()
        {
            await NewSeason("First", startOffset: 0, lengthDays: 60);

            var backwards = await _client.PostAsJsonAsync("/kc/api/seasons", new { Name = "Backwards", PlannedStart = Today.AddDays(10), PlannedEnd = Today });
            var overlapping = await _client.PostAsJsonAsync("/kc/api/seasons", new { Name = "Overlap", PlannedStart = Today.AddDays(30), PlannedEnd = Today.AddDays(90) });
            var backToBack = await _client.PostAsJsonAsync("/kc/api/seasons", new { Name = "Next", PlannedStart = Today.AddDays(60), PlannedEnd = Today.AddDays(120) });

            Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, overlapping.StatusCode);
            Assert.Equal(HttpStatusCode.Created, backToBack.StatusCode);
        }

        [DbFact]
        public async Task Update_MergesFieldsAndClearsBlankOnes()
        {
            var season = await NewSeason();
            await _client.PutAsJsonAsync($"/kc/api/seasons/{season.SeasonId}", new { Intention = "Go deep" });

            var res = await _client.PutAsJsonAsync($"/kc/api/seasons/{season.SeasonId}", new { Name = "Renamed", Intention = "" });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var updated = (await res.Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
            Assert.Equal("Renamed", updated.Name);
            Assert.Null(updated.Intention);
            Assert.Equal(season.PlannedEnd, updated.PlannedEnd);
        }

        [DbFact]
        public async Task GetAll_FiltersByStatus()
        {
            var a = await NewSeason("A", 0);
            await NewSeason("B", 60);
            await PutLineup(a.SeasonId, Lineup(SeedDomains(1)));
            await _client.PostAsync($"/kc/api/seasons/{a.SeasonId}/start", null);

            var active = await _client.GetFromJsonAsync<List<Season>>("/kc/api/seasons?status=Active");
            var all = await _client.GetFromJsonAsync<List<Season>>("/kc/api/seasons");
            var bad = await _client.GetAsync("/kc/api/seasons?status=Someday");

            Assert.Equal(a.SeasonId, Assert.Single(active!).SeasonId);
            Assert.Equal(new[] { "A", "B" }, all!.Select(s => s.Name));
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }

        [DbFact]
        public async Task Delete_RemovesSeasonButKeepsDomains()
        {
            var season = await NewSeason();
            var domains = SeedDomains(2);
            await PutLineup(season.SeasonId, Lineup(domains));

            var res = await _client.DeleteAsync($"/kc/api/seasons/{season.SeasonId}");

            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
            Assert.Equal(0, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""SeasonDomains"" WHERE ""SeasonId"" = {season.SeasonId};"));
            Assert.Equal(2, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Domains"" WHERE ""DomainId"" IN ({string.Join(',', domains)});"));
        }

        /* ===================== LINEUP ===================== */

        [DbFact]
        public async Task SetDomains_SavesUpToTwoFocusWithoutAsking()
        {
            var season = await NewSeason();
            var focus = SeedDomains(2);
            var passive = SeedDomains(4);

            var res = await PutLineup(season.SeasonId, Lineup(focus, passive));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var saved = (await res.Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
            Assert.Equal(2, saved.FocusCount);
            Assert.Equal(4, saved.PassiveCount);
            Assert.Equal(new[] { "Focus", "Focus", "Passive", "Passive", "Passive", "Passive" }, saved.Domains.Select(d => d.Role));
        }

        [DbFact]
        public async Task SetDomains_ReplacesTheWholeLineup()
        {
            var season = await NewSeason();
            var domains = SeedDomains(3);
            await PutLineup(season.SeasonId, Lineup(new[] { domains[0] }, new[] { domains[1] }));

            var res = await PutLineup(season.SeasonId, Lineup(new[] { domains[1] }, new[] { domains[2] }));

            var saved = (await res.Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
            Assert.Equal(new[] { (domains[1], "Focus"), (domains[2], "Passive") }, saved.Domains.Select(d => (d.DomainId, d.Role)));
        }

        [DbFact]
        public async Task SetDomains_RejectsMissingArchivedDuplicateOrBadRole()
        {
            var season = await NewSeason();
            int ok = SeedDomain("Ok"), archived = SeedDomain("Old", archived: true);

            var missing = await PutLineup(season.SeasonId, Lineup(new[] { 999999 }));
            var intoArchived = await PutLineup(season.SeasonId, Lineup(new[] { archived }));
            var duplicate = await PutLineup(season.SeasonId, Lineup(new[] { ok }, new[] { ok }));
            var badRole = await PutLineup(season.SeasonId, new { Domains = new[] { new { DomainId = ok, Role = "Obsession" } } });

            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, intoArchived.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
            Assert.Equal(0, FocusCountInDb(season.SeasonId));
        }

        [DbFact]
        public async Task SetDomains_KeepsADomainArchivedMidSeason()
        {
            var season = await NewSeason();
            int domain = SeedDomain("Archived later");
            await PutLineup(season.SeasonId, Lineup(new[] { domain }));
            TestDatabase.Scalar($@"UPDATE ""Domains"" SET ""IsArchived"" = TRUE WHERE ""DomainId"" = {domain} RETURNING 1;");

            var res = await PutLineup(season.SeasonId, Lineup(new[] { domain }, SeedDomains(1)));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True((await res.Content.ReadFromJsonAsync<SeasonDetailsDto>())!.Domains.Single(d => d.DomainId == domain).IsArchived);
        }

        /* ===================== FOCUS OVERLOAD ===================== */

        [DbFact]
        public async Task Overload_ThirdFocusWarnsThenSavesOnConfirm()
        {
            var season = await NewSeason();
            var focus = SeedDomains(3);

            var warning = await Warning(await PutLineup(season.SeasonId, Lineup(focus)));
            Assert.Equal(1, warning.Level);
            Assert.Equal(3, warning.FocusCount);
            Assert.Null(warning.AckError);
            Assert.Equal(0, FocusCountInDb(season.SeasonId));

            var confirmed = await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken }));

            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.Equal(3, FocusCountInDb(season.SeasonId));
        }

        [DbFact]
        public async Task Overload_FourthFocusNeedsThePhrase()
        {
            var season = await NewSeason();
            var focus = SeedDomains(4);

            var warning = await Warning(await PutLineup(season.SeasonId, Lineup(focus)));
            Assert.Equal(2, warning.Level);

            var wrong = await Warning(await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken, Phrase = "yes" })));
            Assert.NotNull(wrong.AckError);
            Assert.Equal(warning.ChallengeToken, wrong.ChallengeToken);

            var right = await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken, Phrase = warning.Requires.Phrase }));
            Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        }

        [DbFact]
        public async Task Overload_FifthFocusNeedsAReasonWhichIsKept()
        {
            var season = await NewSeason();
            var focus = SeedDomains(5);
            string reason = "A certification exam lands this season and work needs two of these, so they all overlap for a few weeks.";

            var warning = await Warning(await PutLineup(season.SeasonId, Lineup(focus)));
            Assert.Equal(3, warning.Level);

            var noReason = await Warning(await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken, Phrase = warning.Requires.Phrase })));
            Assert.NotNull(noReason.AckError);

            var res = await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken, Phrase = warning.Requires.Phrase, Reason = reason }));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(reason, (await res.Content.ReadFromJsonAsync<SeasonDetailsDto>())!.OverloadReason);

            // Back within capacity, the reason no longer applies.
            var trimmed = await PutLineup(season.SeasonId, Lineup(focus.Take(2)));
            Assert.Null((await trimmed.Content.ReadFromJsonAsync<SeasonDetailsDto>())!.OverloadReason);
        }

        [DbFact]
        public async Task Overload_SixthFocusMakesYouWait()
        {
            var season = await NewSeason();
            var focus = SeedDomains(6);

            var warning = await Warning(await PutLineup(season.SeasonId, Lineup(focus)));
            Assert.Equal(4, warning.Level);
            Assert.Equal(60, warning.Requires.WaitSeconds);

            var tooSoon = await Warning(await PutLineup(season.SeasonId, Lineup(focus, ack: new
            {
                warning.ChallengeToken,
                Phrase = warning.Requires.Phrase,
                Reason = new string('x', warning.Requires.ReasonMinLength)
            })));

            Assert.NotNull(tooSoon.AckError);
            Assert.Equal(warning.ChallengeToken, tooSoon.ChallengeToken);
            Assert.InRange(tooSoon.Requires.WaitRemainingSeconds, 1, 60);
            Assert.Equal(0, FocusCountInDb(season.SeasonId));
        }

        [DbFact]
        public async Task Overload_ConfirmationDoesNotCarryOverToABiggerLineup()
        {
            var season = await NewSeason();
            var focus = SeedDomains(4);

            var warning = await Warning(await PutLineup(season.SeasonId, Lineup(focus.Take(3))));
            var reused = await Warning(await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken, Phrase = "I accept slower progress on everything" })));

            Assert.NotNull(reused.AckError);
            Assert.NotEqual(warning.ChallengeToken, reused.ChallengeToken);
            Assert.Equal(0, FocusCountInDb(season.SeasonId));
        }

        [DbFact]
        public async Task Overload_OnlyAddingAFocusAsksAgain()
        {
            var season = await NewSeason();
            var focus = SeedDomains(3);
            var warning = await Warning(await PutLineup(season.SeasonId, Lineup(focus)));
            await PutLineup(season.SeasonId, Lineup(focus, ack: new { warning.ChallengeToken }));

            var passiveAdded = await PutLineup(season.SeasonId, Lineup(focus, SeedDomains(1)));
            var swapped = await PutLineup(season.SeasonId, Lineup(new[] { focus[0], focus[1], SeedDomain("Swapped in") }));

            Assert.Equal(HttpStatusCode.OK, passiveAdded.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, swapped.StatusCode);
        }

        /* ===================== LIFECYCLE ===================== */

        [DbFact]
        public async Task Start_NeedsAFocusDomain()
        {
            var season = await NewSeason();
            await PutLineup(season.SeasonId, Lineup(Array.Empty<int>(), SeedDomains(1)));

            var res = await _client.PostAsync($"/kc/api/seasons/{season.SeasonId}/start", null);

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        [DbFact]
        public async Task Start_OnlyOneSeasonCanBeActive()
        {
            var first = await NewSeason("First", 0);
            var second = await NewSeason("Second", 60);
            await PutLineup(first.SeasonId, Lineup(SeedDomains(1)));
            await PutLineup(second.SeasonId, Lineup(SeedDomains(1)));

            var started = await _client.PostAsync($"/kc/api/seasons/{first.SeasonId}/start", null);
            var blocked = await _client.PostAsync($"/kc/api/seasons/{second.SeasonId}/start", null);
            var again = await _client.PostAsync($"/kc/api/seasons/{first.SeasonId}/start", null);

            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            var active = (await started.Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
            Assert.Equal("Active", active.Status);
            Assert.NotNull(active.StartedAt);
            Assert.NotNull(active.Stats);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        [DbFact]
        public async Task OneActive_IsEnforcedByTheDatabase()
        {
            await NewSeason("First", 0);
            await NewSeason("Second", 60);

            TestDatabase.Scalar(@"UPDATE ""Seasons"" SET ""Status"" = 'Active' WHERE ""Name"" = 'First' RETURNING 1;");

            Assert.ThrowsAny<Npgsql.PostgresException>(() =>
                TestDatabase.Scalar(@"UPDATE ""Seasons"" SET ""Status"" = 'Active' WHERE ""Name"" = 'Second' RETURNING 1;"));
        }

        [DbFact]
        public async Task End_RecordsReflectionAndLocksTheLineup()
        {
            var season = await NewSeason();
            var domains = SeedDomains(2);
            await PutLineup(season.SeasonId, Lineup(domains));
            await _client.PostAsync($"/kc/api/seasons/{season.SeasonId}/start", null);

            var ended = await _client.PostAsJsonAsync($"/kc/api/seasons/{season.SeasonId}/end", new { Reflection = "Went well." });
            var lineup = await PutLineup(season.SeasonId, Lineup(domains.Take(1)));
            var dates = await _client.PutAsJsonAsync($"/kc/api/seasons/{season.SeasonId}", new { PlannedEnd = Today.AddDays(90) });
            var reflection = await _client.PutAsJsonAsync($"/kc/api/seasons/{season.SeasonId}", new { Reflection = "Went well, in hindsight." });
            var current = await _client.GetAsync("/kc/api/seasons/current");

            Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
            var body = (await ended.Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
            Assert.Equal("Ended", body.Status);
            Assert.Equal("Went well.", body.Reflection);
            Assert.NotNull(body.EndedAt);
            Assert.Equal(HttpStatusCode.Conflict, lineup.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, dates.StatusCode);
            Assert.Equal(HttpStatusCode.OK, reflection.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        }

        [DbFact]
        public async Task End_OnlyTheActiveSeason()
        {
            var season = await NewSeason();

            var res = await _client.PostAsync($"/kc/api/seasons/{season.SeasonId}/end", null);

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        [DbFact]
        public async Task Rotate_EndsCurrentAndStartsNextTogether()
        {
            var current = await NewSeason("Current", 0);
            var next = await NewSeason("Next", 60);
            await PutLineup(current.SeasonId, Lineup(SeedDomains(1)));
            await PutLineup(next.SeasonId, Lineup(SeedDomains(2)));
            await _client.PostAsync($"/kc/api/seasons/{current.SeasonId}/start", null);

            var res = await _client.PostAsJsonAsync($"/kc/api/seasons/{next.SeasonId}/rotate", new { Reflection = "Time to switch." });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var nowActive = await _client.GetFromJsonAsync<SeasonDetailsDto>("/kc/api/seasons/current");
            var old = await _client.GetFromJsonAsync<SeasonDetailsDto>($"/kc/api/seasons/{current.SeasonId}");
            Assert.Equal(next.SeasonId, nowActive!.SeasonId);
            Assert.Equal("Ended", old!.Status);
            Assert.Equal("Time to switch.", old.Reflection);
        }

        [DbFact]
        public async Task Rotate_NeedsAnActiveSeasonAndAPlannedTargetWithFocus()
        {
            var current = await NewSeason("Current", 0);
            var empty = await NewSeason("Empty", 60);
            await PutLineup(current.SeasonId, Lineup(SeedDomains(1)));

            var nothingActive = await _client.PostAsync($"/kc/api/seasons/{current.SeasonId}/rotate", null);
            await _client.PostAsync($"/kc/api/seasons/{current.SeasonId}/start", null);
            var noFocus = await _client.PostAsync($"/kc/api/seasons/{empty.SeasonId}/rotate", null);
            var toItself = await _client.PostAsync($"/kc/api/seasons/{current.SeasonId}/rotate", null);

            Assert.Equal(HttpStatusCode.Conflict, nothingActive.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, noFocus.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, toItself.StatusCode);
            Assert.Equal("Active", (await _client.GetFromJsonAsync<SeasonDetailsDto>($"/kc/api/seasons/{current.SeasonId}"))!.Status);
        }

        [DbFact]
        public async Task Lifecycle_MissingSeasonReturns404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/kc/api/seasons/999999")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/kc/api/seasons/999999/start", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await PutLineup(999999, Lineup(SeedDomains(1)))).StatusCode);
        }

        /* ===================== STATS ===================== */

        [DbFact]
        public async Task Stats_CountTheSeasonWindowByRole()
        {
            int focus = SeedDomain("Focus domain"), passive = SeedDomain("Passive domain"), outside = SeedDomain("Outside domain");
            int focusNode = SeedNode(focus, "Focus node"), passiveNode = SeedNode(passive, "Passive node"), outsideNode = SeedNode(outside, "Outside node");

            var season = await NewSeason();
            await PutLineup(season.SeasonId, Lineup(new[] { focus }, new[] { passive }));

            // Before the season starts: must not count.
            TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({focusNode}, now() - interval '30 days', 'Old', 'Old') RETURNING ""LogId"";");

            await _client.PostAsync($"/kc/api/seasons/{season.SeasonId}/start", null);
            // Pin activity just after StartedAt so it's inside the window no
            // matter how the test host's clock relates to AppClock's zone.
            string at = "(SELECT \"StartedAt\" FROM \"Seasons\" WHERE \"SeasonId\" = " + season.SeasonId + ") + interval '1 second'";

            for (int i = 0; i < 3; i++)
                TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({focusNode}, {at}, 'Log', 'Body') RETURNING ""LogId"";");
            TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({outsideNode}, {at}, 'Log', 'Body') RETURNING ""LogId"";");
            TestDatabase.Scalar($@"INSERT INTO ""Actions"" (""KnowledgeNodeId"", ""ActionText"", ""Status"", ""CreatedAt"", ""CompletedAt"") VALUES ({passiveNode}, 'Do', 'Completed', {at}, {at}) RETURNING ""Id"";");
            int session = TestDatabase.Scalar($@"INSERT INTO ""LearnSessions"" (""Title"", ""NodeId"") VALUES ('Learn', {focusNode}) RETURNING ""SessionId"";");
            int unfiled = TestDatabase.Scalar(@"INSERT INTO ""LearnSessions"" (""Title"") VALUES ('Unfiled') RETURNING ""SessionId"";");
            TestDatabase.Scalar($@"INSERT INTO ""LearnEntries"" (""SessionId"", ""Position"", ""Source"", ""CreatedAt"") VALUES ({session}, 65536, 'note', {at}) RETURNING ""EntryId"";");
            TestDatabase.Scalar($@"INSERT INTO ""LearnEntries"" (""SessionId"", ""Position"", ""Source"", ""CreatedAt"") VALUES ({unfiled}, 65536, 'note', {at}) RETURNING ""EntryId"";");

            // End it slightly later so everything above is inside the window.
            // Ended straight in SQL, so there's no snapshot: this also covers the
            // live fallback for seasons ended before snapshots existed.
            TestDatabase.Scalar($@"UPDATE ""Seasons"" SET ""Status"" = 'Ended', ""EndedAt"" = ""StartedAt"" + interval '1 minute' WHERE ""SeasonId"" = {season.SeasonId} RETURNING 1;");

            var stats = (await _client.GetFromJsonAsync<SeasonDetailsDto>($"/kc/api/seasons/{season.SeasonId}"))!.Stats!;

            Assert.Equal(4, stats.Totals.Logs);
            Assert.Equal(1, stats.Totals.ActionsCompleted);
            Assert.Equal(2, stats.Totals.LearnEntries);
            Assert.Equal(2, stats.Totals.LearnSessions);
            Assert.Equal(1, stats.UnfiledLearnEntries);
            Assert.Equal(1, stats.ActiveDays);

            var byRole = stats.ByDomain.ToDictionary(d => d.DomainId);
            Assert.Equal(("Focus", 4), (byRole[focus].Role, byRole[focus].Score));     // 3 logs + 1 learn entry
            Assert.Equal(("Passive", 1), (byRole[passive].Role, byRole[passive].Score));
            Assert.Equal(("Outside", 1), (byRole[outside].Role, byRole[outside].Score));
            Assert.Equal(66.7, stats.FocusShare);

            Assert.Equal(focusNode, stats.TopKnowledgeNodes[0].NodeId);
        }

        // Starts the season, backdates its start an hour so activity pinned just
        // after it is safely inside the window by the time it ends, and returns
        // that pinned moment as SQL.
        private async Task<string> StartBackdated(int seasonId)
        {
            await _client.PostAsync($"/kc/api/seasons/{seasonId}/start", null);
            TestDatabase.Scalar($@"UPDATE ""Seasons"" SET ""StartedAt"" = ""StartedAt"" - interval '1 hour' WHERE ""SeasonId"" = {seasonId} RETURNING 1;");
            return $@"(SELECT ""StartedAt"" FROM ""Seasons"" WHERE ""SeasonId"" = {seasonId}) + interval '1 second'";
        }

        [DbFact]
        public async Task Stats_EndedSeasonKeepsItsStatsWhenANodeMoves()
        {
            int focus = SeedDomain("Kept focus"), outside = SeedDomain("Moved to");
            int node = SeedNode(focus, "Moving node");
            var season = await NewSeason();
            await PutLineup(season.SeasonId, Lineup(new[] { focus }));
            string at = await StartBackdated(season.SeasonId);
            TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({node}, {at}, 'Log', 'Body') RETURNING ""LogId"";");

            var ended = (await (await _client.PostAsync($"/kc/api/seasons/{season.SeasonId}/end", null))
                .Content.ReadFromJsonAsync<SeasonDetailsDto>())!;
            TestDatabase.Scalar($@"UPDATE ""KnowledgeNodes"" SET ""DomainId"" = {outside} WHERE ""Id"" = {node} RETURNING 1;");
            var later = (await _client.GetFromJsonAsync<SeasonDetailsDto>($"/kc/api/seasons/{season.SeasonId}"))!;

            Assert.Equal(1, ended.Stats!.ByDomain.Single(d => d.DomainId == focus).Score);
            Assert.Equal(100, later.Stats!.FocusShare);
            Assert.Equal(("Focus", 1), (later.Stats.ByDomain.Single().Role, later.Stats.ByDomain.Single().Score));
            Assert.Equal(focus, later.Stats.TopKnowledgeNodes[0].DomainId);
            // Postgres keeps microseconds, so EndedAt can be a tick or two shy of the snapshot's To.
            Assert.True(Math.Abs((later.Stats.To - ended.EndedAt!.Value).TotalMilliseconds) < 1);
        }

        [DbFact]
        public async Task Stats_RotateSnapshotsOnlyTheSeasonBeingEnded()
        {
            int focus = SeedDomain("Rotating focus");
            int node = SeedNode(focus, "Rotating node");
            var current = await NewSeason("Current", 0);
            var next = await NewSeason("Next", 60);
            await PutLineup(current.SeasonId, Lineup(new[] { focus }));
            await PutLineup(next.SeasonId, Lineup(SeedDomains(1)));
            string at = await StartBackdated(current.SeasonId);
            TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({node}, {at}, 'Log', 'Body') RETURNING ""LogId"";");

            await _client.PostAsync($"/kc/api/seasons/{next.SeasonId}/rotate", null);
            TestDatabase.Scalar($@"DELETE FROM ""LogEntries"" WHERE ""NodeId"" = {node} RETURNING 1;");
            var old = (await _client.GetFromJsonAsync<SeasonDetailsDto>($"/kc/api/seasons/{current.SeasonId}"))!;

            Assert.Equal(1, old.Stats!.Totals.Logs);
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Seasons"" WHERE ""SeasonId"" = {current.SeasonId} AND ""StatsSnapshot"" IS NOT NULL;"));
            Assert.Equal(0, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Seasons"" WHERE ""SeasonId"" = {next.SeasonId} AND ""StatsSnapshot"" IS NOT NULL;"));
        }
    }

    /*
     * Demo mode never touches the database, so these run with or without KC_TEST_DB.
     */
    public class SeasonDemoModeTests : IClassFixture<TestApiFactory>
    {
        private readonly HttpClient _client;

        public SeasonDemoModeTests(TestApiFactory factory)
        {
            _client = factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDemoToken(TestApiFactory.JwtSecret));
        }

        [Fact]
        public async Task Demo_ListsSeasonsAndShowsTheCurrentOne()
        {
            var seasons = await _client.GetFromJsonAsync<List<Season>>("/kc/api/seasons");
            var current = await _client.GetFromJsonAsync<SeasonDetailsDto>("/kc/api/seasons/current");

            Assert.Equal(new[] { "Ended", "Active", "Planned" }, seasons!.Select(s => s.Status));
            Assert.Equal("Active", current!.Status);
            Assert.NotNull(current.Stats);
            Assert.Contains(current.Domains, d => d.Role == "Passive");
            // The demo Learn session is filed under a Databases node, a focus this season.
            Assert.True(current.Stats!.Totals.LearnEntries > 0);
        }

        [Fact]
        public async Task Demo_EndedSeasonHasStatsAndPlannedHasNone()
        {
            var ended = await _client.GetFromJsonAsync<SeasonDetailsDto>("/kc/api/seasons/1");
            var planned = await _client.GetFromJsonAsync<SeasonDetailsDto>("/kc/api/seasons/3");

            Assert.True(ended!.Stats!.Totals.Logs > 0);
            Assert.NotEmpty(ended.Stats.TopKnowledgeNodes);
            Assert.Null(planned!.Stats);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/kc/api/seasons/99")).StatusCode);
        }

        [Fact]
        public async Task Demo_WritesAreFakedOrBlocked()
        {
            var create = await _client.PostAsJsonAsync("/kc/api/seasons", new { Name = "Demo", PlannedStart = "2026-11-01", PlannedEnd = "2027-01-01" });
            var lineup = await _client.PutAsJsonAsync("/kc/api/seasons/3/domains", new { Domains = new[] { new { DomainId = 1, Role = "Focus" } } });
            var start = await _client.PostAsync("/kc/api/seasons/3/start", null);

            Assert.Equal(HttpStatusCode.OK, create.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, lineup.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
        }
    }
}
