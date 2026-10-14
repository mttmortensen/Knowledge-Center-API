using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Knowledge_Center_API.Models.Learn;
using Knowledge_Center_API.Services.Core;
using Knowledge_Center_API.Services.Security;

namespace Knowledge_Center_API.Tests
{
    /*
     * Rebuilds the KC_TEST_DB schema once for the class and seeds one domain, one
     * Knowledge Node (for the NodeId label) and one log (to prove Learn leaves
     * Logs alone, and to link entries to).
     */
    public class LearnDbFixture : IDisposable
    {
        public TestApiFactory Factory { get; } = new();
        public int NodeId { get; }
        public int LogId { get; }

        public LearnDbFixture()
        {
            if (string.IsNullOrWhiteSpace(TestDatabase.ConnectionString)) return;

            TestDatabase.Reset();
            int domainId = TestDatabase.Scalar(@"INSERT INTO ""Domains"" (""DomainName"", ""DomainStatus"") VALUES ('Test domain', 'Active') RETURNING ""DomainId"";");
            // Every column filled: the existing KnowledgeNode read path assumes non-null values.
            NodeId = TestDatabase.Scalar($@"INSERT INTO ""KnowledgeNodes"" (""Title"", ""Description"", ""ConfidenceLevel"", ""Status"", ""CreatedAt"", ""LastUpdated"", ""DomainId"") VALUES ('Test node', 'Seeded for tests', 1, 'Learning', now(), now(), {domainId}) RETURNING ""Id"";");
            LogId = TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({NodeId}, now(), 'Seeded log', 'Seeded log body') RETURNING ""LogId"";");
        }

        // One session token for the whole class: AuthSession keeps one token per
        // username, so creating another would invalidate clients already handed out.
        private readonly string _token = AuthSession.CreateSession("learn-tests");

        public HttpClient CreateClient()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return client;
        }

        public void Dispose() => Factory.Dispose();
    }

    public class LearnEndpointTests : IClassFixture<LearnDbFixture>
    {
        private readonly LearnDbFixture _fx;

        public LearnEndpointTests(LearnDbFixture fx)
        {
            _fx = fx;
        }

        /* ===================== HELPERS ===================== */

        private async Task<(HttpClient client, LearnSessionDetailsDto session)> NewSession(string title = "Session")
        {
            var client = _fx.CreateClient();
            var res = await client.PostAsJsonAsync("/kc/api/learn/sessions", new { Title = title, Topic = "Testing" });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return (client, (await res.Content.ReadFromJsonAsync<LearnSessionDetailsDto>())!);
        }

        private static async Task<LearnEntry> AddEntry(HttpClient client, int sessionId, string source, int? beforeEntryId = null)
        {
            var res = await client.PostAsJsonAsync($"/kc/api/learn/sessions/{sessionId}/entries", new { Source = source, BeforeEntryId = beforeEntryId });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return (await res.Content.ReadFromJsonAsync<LearnEntry>())!;
        }

        private static async Task<LearnSessionDetailsDto> GetSession(HttpClient client, int sessionId) =>
            (await client.GetFromJsonAsync<LearnSessionDetailsDto>($"/kc/api/learn/sessions/{sessionId}"))!;

        private static async Task<string> ErrorMessage(HttpResponseMessage res) =>
            (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"];

        /* ===================== SESSIONS ===================== */

        [DbFact]
        public async Task Session_CrudRoundTrip()
        {
            var (client, created) = await NewSession("Original");
            Assert.Empty(created.Entries);
            Assert.Equal("Testing", created.Topic);

            var list = await client.GetFromJsonAsync<List<LearnSession>>("/kc/api/learn/sessions");
            Assert.Contains(list!, s => s.SessionId == created.SessionId);

            var put = await client.PutAsJsonAsync($"/kc/api/learn/sessions/{created.SessionId}",
                new { Title = "Renamed", Topic = "", NodeId = _fx.NodeId });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = await put.Content.ReadFromJsonAsync<LearnSessionDetailsDto>();
            Assert.Equal("Renamed", updated!.Title);
            Assert.Null(updated.Topic);
            Assert.Equal(_fx.NodeId, updated.NodeId);
            Assert.Equal("Test node", updated.NodeTitle);

            await AddEntry(client, created.SessionId, "note");
            var del = await client.DeleteAsync($"/kc/api/learn/sessions/{created.SessionId}");
            Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/kc/api/learn/sessions/{created.SessionId}")).StatusCode);
            Assert.Equal(0, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""LearnEntries"" WHERE ""SessionId"" = {created.SessionId}"));
        }

        [DbFact]
        public async Task Session_RejectsMissingTitleAndUnknownNode()
        {
            var client = _fx.CreateClient();

            var noTitle = await client.PostAsJsonAsync("/kc/api/learn/sessions", new { Title = " " });
            Assert.Equal(HttpStatusCode.BadRequest, noTitle.StatusCode);

            var badNode = await client.PostAsJsonAsync("/kc/api/learn/sessions", new { Title = "x", NodeId = 999999 });
            Assert.Equal(HttpStatusCode.BadRequest, badNode.StatusCode);
        }

        /* ===================== ENTRIES & ORDERING ===================== */

        [DbFact]
        public async Task Entries_AppendAndInsertBetween()
        {
            var (client, session) = await NewSession();
            var a = await AddEntry(client, session.SessionId, "A");
            var c = await AddEntry(client, session.SessionId, "C");
            var b = await AddEntry(client, session.SessionId, "B", beforeEntryId: c.EntryId);
            var first = await AddEntry(client, session.SessionId, "0", beforeEntryId: a.EntryId);

            Assert.True(a.Position < b.Position && b.Position < c.Position);

            var entries = (await GetSession(client, session.SessionId)).Entries;
            Assert.Equal(new[] { "0", "A", "B", "C" }, entries.Select(e => e.Source));
            Assert.Equal(first.EntryId, entries[0].EntryId);
        }

        [DbFact]
        public async Task Entries_RepeatedInsertsIntoOneGapRenumberAndKeepOrder()
        {
            var (client, session) = await NewSession();
            await AddEntry(client, session.SessionId, "start");
            var end = await AddEntry(client, session.SessionId, "end");

            // Each insert lands directly above "end", halving the same gap every time;
            // 40 inserts is well past the ~16 a 65536 gap can absorb.
            var expected = new List<string> { "start" };
            for (int i = 0; i < 40; i++)
            {
                await AddEntry(client, session.SessionId, $"n{i}", beforeEntryId: end.EntryId);
                expected.Add($"n{i}");
            }
            expected.Add("end");

            var entries = (await GetSession(client, session.SessionId)).Entries;
            Assert.Equal(expected, entries.Select(e => e.Source));
            Assert.Equal(entries.Count, entries.Select(e => e.Position).Distinct().Count());
        }

        [DbFact]
        public async Task Entries_RejectEmptySourceAndForeignBeforeEntry()
        {
            var (client, session) = await NewSession();
            var (_, other) = await NewSession("Other");
            var foreign = await AddEntry(client, other.SessionId, "elsewhere");

            var empty = await client.PostAsJsonAsync($"/kc/api/learn/sessions/{session.SessionId}/entries", new { Source = "   " });
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

            var wrongSession = await client.PostAsJsonAsync($"/kc/api/learn/sessions/{session.SessionId}/entries",
                new { Source = "x", BeforeEntryId = foreign.EntryId });
            Assert.Equal(HttpStatusCode.BadRequest, wrongSession.StatusCode);
        }

        [DbFact]
        public async Task Entries_UpdateAndDelete()
        {
            var (client, session) = await NewSession();
            var entry = await AddEntry(client, session.SessionId, "draft");

            var put = await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}", new { Source = "?? now a question" });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = await put.Content.ReadFromJsonAsync<LearnEntry>();
            Assert.Equal("question", updated!.EntryType);
            Assert.Equal("open", updated.QuestionStatus);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/kc/api/learn/entries/{entry.EntryId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/kc/api/learn/entries/{entry.EntryId}")).StatusCode);
        }

        /* ===================== @answers ===================== */

        [DbFact]
        public async Task Answers_FollowUpMarksQuestionAnsweredAndClearsItFromOpenQuestions()
        {
            var (client, session) = await NewSession();
            var question = await AddEntry(client, session.SessionId, "?? what is MVCC");
            Assert.Equal("open", question.QuestionStatus);

            var open = await client.GetFromJsonAsync<List<LearnOpenQuestionDto>>($"/kc/api/learn/questions/open?sessionId={session.SessionId}");
            Assert.Single(open!, q => q.EntryId == question.EntryId);

            var followUp = await AddEntry(client, session.SessionId, $"@answers({question.EntryId}) multi-version concurrency control");
            Assert.Equal("followup", followUp.EntryType);
            Assert.Equal(question.EntryId, followUp.AnswersEntryId);

            var entries = (await GetSession(client, session.SessionId)).Entries;
            Assert.Equal("answered", entries.Single(e => e.EntryId == question.EntryId).QuestionStatus);

            open = await client.GetFromJsonAsync<List<LearnOpenQuestionDto>>($"/kc/api/learn/questions/open?sessionId={session.SessionId}");
            Assert.Empty(open!);
        }

        [DbFact]
        public async Task Answers_RejectsMissingSelfAndCrossSessionTargets()
        {
            var (client, session) = await NewSession();
            var (_, other) = await NewSession("Other");
            var foreignQuestion = await AddEntry(client, other.SessionId, "?? elsewhere");
            var entry = await AddEntry(client, session.SessionId, "note");

            var missing = await client.PostAsJsonAsync($"/kc/api/learn/sessions/{session.SessionId}/entries", new { Source = "@answers(999999) x" });
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.Contains("does not exist", await ErrorMessage(missing));

            var cross = await client.PostAsJsonAsync($"/kc/api/learn/sessions/{session.SessionId}/entries",
                new { Source = $"@answers({foreignQuestion.EntryId}) x" });
            Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);
            Assert.Contains("different session", await ErrorMessage(cross));

            var self = await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}", new { Source = $"@answers({entry.EntryId}) me" });
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        }

        [DbFact]
        public async Task Answers_FollowUpSurvivesItsQuestionBeingDeleted()
        {
            var (client, session) = await NewSession();
            var question = await AddEntry(client, session.SessionId, "?? temporary");
            var followUp = await AddEntry(client, session.SessionId, $"@answers({question.EntryId}) answer");

            await client.DeleteAsync($"/kc/api/learn/entries/{question.EntryId}");

            var entries = (await GetSession(client, session.SessionId)).Entries;
            var orphan = Assert.Single(entries);
            Assert.Equal(question.EntryId, orphan.AnswersEntryId);

            // Editing the text without changing the (now dangling) reference still works.
            var edit = await client.PutAsJsonAsync($"/kc/api/learn/entries/{followUp.EntryId}",
                new { Source = $"@answers({question.EntryId}) answer, revised" });
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        }

        /* ===================== LOGS & EXPORT ===================== */

        [DbFact]
        public async Task Learn_NeverTouchesLogs()
        {
            var (client, session) = await NewSession();
            string countLogs = @"SELECT COUNT(*) FROM ""LogEntries""";
            int logsBefore = TestDatabase.Scalar(countLogs);

            // "@log..." is no longer special: it saves as ordinary, unvalidated text.
            var reference = await AddEntry(client, session.SessionId, "@log(999999)");
            Assert.Equal("note", reference.EntryType);
            await AddEntry(client, session.SessionId, "@log a quick note");

            Assert.Equal(logsBefore, TestDatabase.Scalar(countLogs));
        }

        [DbFact]
        public async Task LogLink_SetsAndClearsWithoutTouchingTheLog()
        {
            var (client, session) = await NewSession();
            var entry = await AddEntry(client, session.SessionId, "worth keeping");
            Assert.Null(entry.LogId);
            string logTitle = $@"SELECT COUNT(*) FROM ""LogEntries"" WHERE ""LogId"" = {_fx.LogId} AND ""Title"" = 'Seeded log'";

            var set = await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}/log", new { LogId = _fx.LogId });
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
            Assert.Equal(_fx.LogId, (await set.Content.ReadFromJsonAsync<LearnEntry>())!.LogId);
            Assert.Equal(_fx.LogId, (await GetSession(client, session.SessionId)).Entries.Single().LogId);
            Assert.Equal(1, TestDatabase.Scalar(logTitle));

            // Editing the source keeps the link.
            await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}", new { Source = "still worth keeping" });
            Assert.Equal(_fx.LogId, (await GetSession(client, session.SessionId)).Entries.Single().LogId);

            var clear = await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}/log", new { LogId = 0 });
            Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
            Assert.Null((await clear.Content.ReadFromJsonAsync<LearnEntry>())!.LogId);
        }

        [DbFact]
        public async Task LogLink_RejectsUnknownLogAndEntry()
        {
            var (client, session) = await NewSession();
            var entry = await AddEntry(client, session.SessionId, "note");

            var badLog = await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}/log", new { LogId = 999999 });
            Assert.Equal(HttpStatusCode.BadRequest, badLog.StatusCode);
            Assert.Contains("999999", await ErrorMessage(badLog));

            var badEntry = await client.PutAsJsonAsync("/kc/api/learn/entries/999999/log", new { LogId = _fx.LogId });
            Assert.Equal(HttpStatusCode.NotFound, badEntry.StatusCode);
        }

        [DbFact]
        public async Task LogLink_ClearsWhenTheLogIsDeleted()
        {
            var (client, session) = await NewSession();
            var entry = await AddEntry(client, session.SessionId, "short-lived log");
            int logId = TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({_fx.NodeId}, now(), 'Temp', 'Temp') RETURNING ""LogId"";");
            await client.PutAsJsonAsync($"/kc/api/learn/entries/{entry.EntryId}/log", new { LogId = logId });

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/kc/api/logs/{logId}")).StatusCode);

            var after = await GetSession(client, session.SessionId);
            Assert.Null(Assert.Single(after.Entries).LogId);
        }

        [DbFact]
        public async Task Export_ReturnsMarkdownInTimelineOrder()
        {
            var (client, session) = await NewSession("Export me");
            var q = await AddEntry(client, session.SessionId, "?? question");
            var last = await AddEntry(client, session.SessionId, "closing note");
            await AddEntry(client, session.SessionId, $"@answers({q.EntryId}) answer", beforeEntryId: last.EntryId);

            var res = await client.GetAsync($"/kc/api/learn/sessions/{session.SessionId}/export");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("text/markdown", res.Content.Headers.ContentType!.MediaType);
            Assert.Equal("export-me.md", res.Content.Headers.ContentDisposition!.FileName?.Trim('"'));

            string md = await res.Content.ReadAsStringAsync();
            Assert.StartsWith("# Export me", md);
            int question = md.IndexOf("?? question");
            int answer = md.IndexOf($"@answers({q.EntryId}) answer");
            int closing = md.IndexOf("closing note");
            Assert.True(question >= 0 && question < answer && answer < closing);
        }
    }

    /*
     * Demo mode never touches the database, so these run with or without KC_TEST_DB.
     */
    public class LearnDemoModeTests : IClassFixture<TestApiFactory>
    {
        private readonly HttpClient _client;

        public LearnDemoModeTests(TestApiFactory factory)
        {
            _client = factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDemoToken(TestApiFactory.JwtSecret));
        }

        [Fact]
        public async Task Demo_ReadsSeededSession()
        {
            var sessions = await _client.GetFromJsonAsync<List<LearnSession>>("/kc/api/learn/sessions");
            var demo = Assert.Single(sessions!);

            var details = await _client.GetFromJsonAsync<LearnSessionDetailsDto>($"/kc/api/learn/sessions/{demo.SessionId}");
            Assert.Contains(details!.Entries, e => e.EntryType == "followup");
            Assert.Contains(details.Entries, e => e.QuestionStatus == "answered");
            Assert.Contains(details.Entries, e => e.QuestionStatus == "open");
        }

        [Fact]
        public async Task Demo_WritesAreFakedOrBlocked()
        {
            var create = await _client.PostAsJsonAsync("/kc/api/learn/sessions/1/entries", new { Source = "?? demo" });
            Assert.Equal(HttpStatusCode.OK, create.StatusCode);
            Assert.Equal("question", (await create.Content.ReadFromJsonAsync<LearnEntry>())!.EntryType);

            Assert.Equal(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync("/kc/api/learn/entries/1", new { Source = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync("/kc/api/learn/entries/1/log", new { LogId = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.DeleteAsync("/kc/api/learn/entries/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.DeleteAsync("/kc/api/learn/sessions/1")).StatusCode);
        }

        [Fact]
        public async Task Demo_ExportAndOpenQuestions()
        {
            var md = await _client.GetStringAsync("/kc/api/learn/sessions/1/export");
            Assert.StartsWith("# Postgres indexing basics", md);

            var open = await _client.GetFromJsonAsync<List<LearnOpenQuestionDto>>("/kc/api/learn/questions/open");
            Assert.Single(open!);
        }
    }
}
