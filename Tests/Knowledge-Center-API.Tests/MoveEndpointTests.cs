using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Knowledge_Center_API.Services.Security;

namespace Knowledge_Center_API.Tests
{
    /*
     * Moving logs and actions between Knowledge Nodes, and Knowledge Nodes between
     * Domains. Each test seeds its own rows so the tests don't depend on each other.
     */
    public class MoveDbFixture : IDisposable
    {
        public TestApiFactory Factory { get; } = new();

        public MoveDbFixture()
        {
            if (string.IsNullOrWhiteSpace(TestDatabase.ConnectionString)) return;
            TestDatabase.Reset();
        }

        private readonly string _token = AuthSession.CreateSession("move-tests");

        public HttpClient CreateClient()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return client;
        }

        public void Dispose() => Factory.Dispose();
    }

    [Collection(DatabaseCollection.Name)]
    public class MoveEndpointTests : IClassFixture<MoveDbFixture>
    {
        private readonly MoveDbFixture _fx;

        public MoveEndpointTests(MoveDbFixture fx)
        {
            _fx = fx;
        }

        private static int SeedDomain(bool archived = false) =>
            TestDatabase.Scalar($@"INSERT INTO ""Domains"" (""DomainName"", ""DomainStatus"", ""IsArchived"") VALUES ('Domain', 'Active', {archived}) RETURNING ""DomainId"";");

        private static int SeedNode(int domainId, bool archived = false) =>
            TestDatabase.Scalar($@"INSERT INTO ""KnowledgeNodes"" (""Title"", ""Description"", ""ConfidenceLevel"", ""Status"", ""CreatedAt"", ""LastUpdated"", ""DomainId"", ""IsArchived"") VALUES ('Node', 'Desc', 3, 'Learning', now(), now(), {domainId}, {archived}) RETURNING ""Id"";");

        private static int SeedLog(int nodeId) =>
            TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({nodeId}, now(), 'Log', 'Body') RETURNING ""LogId"";");

        private static int SeedAction(int nodeId) =>
            TestDatabase.Scalar($@"INSERT INTO ""Actions"" (""KnowledgeNodeId"", ""ActionText"", ""CreatedAt"") VALUES ({nodeId}, 'Action', now()) RETURNING ""Id"";");

        [DbFact]
        public async Task MoveLog_MovesItWithItsTags()
        {
            int domainId = SeedDomain();
            int from = SeedNode(domainId), to = SeedNode(domainId);
            int logId = SeedLog(from);
            int tagId = TestDatabase.Scalar(@"INSERT INTO ""Tags"" (""Name"") VALUES ('move-tag') RETURNING ""TagId"";");
            TestDatabase.Scalar($@"INSERT INTO ""LogEntryTags"" (""LogId"", ""TagId"") VALUES ({logId}, {tagId}) RETURNING ""LogId"";");

            var res = await _fx.CreateClient().PutAsJsonAsync($"/kc/api/logs/{logId}/move", new { NodeId = to });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(to, TestDatabase.Scalar($@"SELECT ""NodeId"" FROM ""LogEntries"" WHERE ""LogId"" = {logId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""LogEntryTags"" WHERE ""LogId"" = {logId};"));
        }

        [DbFact]
        public async Task MoveLog_ToMissingOrArchivedNode_Returns400AndStays()
        {
            int domainId = SeedDomain();
            int from = SeedNode(domainId), archived = SeedNode(domainId, archived: true);
            int logId = SeedLog(from);
            var client = _fx.CreateClient();

            var missing = await client.PutAsJsonAsync($"/kc/api/logs/{logId}/move", new { NodeId = 999999 });
            var intoArchived = await client.PutAsJsonAsync($"/kc/api/logs/{logId}/move", new { NodeId = archived });

            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, intoArchived.StatusCode);
            Assert.Equal(from, TestDatabase.Scalar($@"SELECT ""NodeId"" FROM ""LogEntries"" WHERE ""LogId"" = {logId};"));
        }

        [DbFact]
        public async Task MoveLog_MissingLog_Returns404()
        {
            int to = SeedNode(SeedDomain());

            var res = await _fx.CreateClient().PutAsJsonAsync("/kc/api/logs/999999/move", new { NodeId = to });

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [DbFact]
        public async Task MoveAction_KeepsItsStatusAndFollowUp()
        {
            int domainId = SeedDomain();
            int from = SeedNode(domainId), to = SeedNode(domainId);
            int actionId = SeedAction(from);
            TestDatabase.Scalar($@"UPDATE ""Actions"" SET ""Status"" = 'Completed', ""CompletedAt"" = now(), ""FollowUp"" = 'Done' WHERE ""Id"" = {actionId} RETURNING ""Id"";");

            var res = await _fx.CreateClient().PutAsJsonAsync($"/kc/api/actions/{actionId}/move", new { KnowledgeNodeId = to });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(to, TestDatabase.Scalar($@"SELECT ""KnowledgeNodeId"" FROM ""Actions"" WHERE ""Id"" = {actionId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Actions"" WHERE ""Id"" = {actionId} AND ""Status"" = 'Completed' AND ""FollowUp"" = 'Done';"));
        }

        [DbFact]
        public async Task MoveAction_ToArchivedNode_Returns400()
        {
            int domainId = SeedDomain();
            int from = SeedNode(domainId), archived = SeedNode(domainId, archived: true);
            int actionId = SeedAction(from);

            var res = await _fx.CreateClient().PutAsJsonAsync($"/kc/api/actions/{actionId}/move", new { KnowledgeNodeId = archived });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal(from, TestDatabase.Scalar($@"SELECT ""KnowledgeNodeId"" FROM ""Actions"" WHERE ""Id"" = {actionId};"));
        }

        [DbFact]
        public async Task MoveNode_MovesItWithItsLogsAndActions()
        {
            int fromDomain = SeedDomain(), toDomain = SeedDomain();
            int nodeId = SeedNode(fromDomain);
            SeedLog(nodeId);
            SeedAction(nodeId);

            var res = await _fx.CreateClient().PutAsJsonAsync($"/kc/api/knowledge-nodes/{nodeId}/move", new { DomainId = toDomain });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(toDomain, TestDatabase.Scalar($@"SELECT ""DomainId"" FROM ""KnowledgeNodes"" WHERE ""Id"" = {nodeId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""LogEntries"" WHERE ""NodeId"" = {nodeId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Actions"" WHERE ""KnowledgeNodeId"" = {nodeId};"));
        }

        [DbFact]
        public async Task MoveNode_ToMissingOrArchivedDomain_Returns400AndStays()
        {
            int fromDomain = SeedDomain(), archivedDomain = SeedDomain(archived: true);
            int nodeId = SeedNode(fromDomain);
            var client = _fx.CreateClient();

            var missing = await client.PutAsJsonAsync($"/kc/api/knowledge-nodes/{nodeId}/move", new { DomainId = 999999 });
            var intoArchived = await client.PutAsJsonAsync($"/kc/api/knowledge-nodes/{nodeId}/move", new { DomainId = archivedDomain });

            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, intoArchived.StatusCode);
            Assert.Equal(fromDomain, TestDatabase.Scalar($@"SELECT ""DomainId"" FROM ""KnowledgeNodes"" WHERE ""Id"" = {nodeId};"));
        }

        [DbFact]
        public async Task UpdateNode_WithMissingDomainId_Returns400AndStays()
        {
            int fromDomain = SeedDomain();
            int nodeId = SeedNode(fromDomain);

            var res = await _fx.CreateClient().PutAsJsonAsync($"/kc/api/knowledge-nodes/{nodeId}", new { DomainId = 999999 });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal(fromDomain, TestDatabase.Scalar($@"SELECT ""DomainId"" FROM ""KnowledgeNodes"" WHERE ""Id"" = {nodeId};"));
        }
    }
}
