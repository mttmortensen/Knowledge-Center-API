using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Knowledge_Center_API.Models.Domains;
using Knowledge_Center_API.Services.Security;

namespace Knowledge_Center_API.Tests
{
    /*
     * Promoting a Knowledge Node to a Domain. Each test seeds its own domain and
     * node (with a log and an action) so the tests don't depend on each other.
     */
    public class PromoteDbFixture : IDisposable
    {
        public TestApiFactory Factory { get; } = new();

        public PromoteDbFixture()
        {
            if (string.IsNullOrWhiteSpace(TestDatabase.ConnectionString)) return;
            TestDatabase.Reset();
        }

        private readonly string _token = AuthSession.CreateSession("promote-tests");

        public HttpClient CreateClient()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return client;
        }

        public void Dispose() => Factory.Dispose();
    }

    public class PromoteEndpointTests : IClassFixture<PromoteDbFixture>
    {
        private readonly PromoteDbFixture _fx;

        public PromoteEndpointTests(PromoteDbFixture fx)
        {
            _fx = fx;
        }

        private static (int domainId, int nodeId) SeedNode(string title, bool archived = false)
        {
            int domainId = TestDatabase.Scalar(@"INSERT INTO ""Domains"" (""DomainName"", ""DomainStatus"") VALUES ('Parent domain', 'Active') RETURNING ""DomainId"";");
            int nodeId = TestDatabase.Scalar($@"INSERT INTO ""KnowledgeNodes"" (""Title"", ""Description"", ""ConfidenceLevel"", ""Status"", ""CreatedAt"", ""LastUpdated"", ""DomainId"", ""IsArchived"") VALUES ('{title}', 'Grew too big for a node', 3, 'Learning', now(), now(), {domainId}, {archived}) RETURNING ""Id"";");
            TestDatabase.Scalar($@"INSERT INTO ""LogEntries"" (""NodeId"", ""EntryDate"", ""Title"", ""Content"") VALUES ({nodeId}, now(), 'Log', 'Body') RETURNING ""LogId"";");
            TestDatabase.Scalar($@"INSERT INTO ""Actions"" (""KnowledgeNodeId"", ""ActionText"", ""CreatedAt"") VALUES ({nodeId}, 'Action', now()) RETURNING ""Id"";");
            return (domainId, nodeId);
        }

        [DbFact]
        public async Task Promote_CreatesDomainAndMovesNodeWithItsLogsAndActions()
        {
            var (oldDomainId, nodeId) = SeedNode("Rust");
            var client = _fx.CreateClient();

            var res = await client.PostAsync($"/kc/api/knowledge-nodes/{nodeId}/promote", null);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);

            var domain = (await res.Content.ReadFromJsonAsync<DomainWithKNsDto>())!;
            Assert.NotEqual(oldDomainId, domain.DomainId);
            Assert.Equal("Rust", domain.DomainName);
            Assert.Equal("Grew too big for a node", domain.DomainDescription);
            Assert.Equal("Active", domain.DomainStatus);
            Assert.Equal(nodeId, Assert.Single(domain.KnowledgeNodes).Id);

            Assert.Equal(0, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""KnowledgeNodes"" WHERE ""DomainId"" = {oldDomainId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""LogEntries"" WHERE ""NodeId"" = {nodeId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Actions"" WHERE ""KnowledgeNodeId"" = {nodeId};"));
        }

        [DbFact]
        public async Task Promote_WithNames_NamesTheDomainAndRenamesTheMovedNode()
        {
            var (_, nodeId) = SeedNode("Kubernetes");
            var client = _fx.CreateClient();

            var res = await client.PostAsJsonAsync($"/kc/api/knowledge-nodes/{nodeId}/promote",
                new { DomainName = "  Containers  ", KnowledgeNodeTitle = "Kubernetes basics" });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);

            var domain = (await res.Content.ReadFromJsonAsync<DomainWithKNsDto>())!;
            Assert.Equal("Containers", domain.DomainName);
            var movedNode = Assert.Single(domain.KnowledgeNodes);
            Assert.Equal(nodeId, movedNode.Id);
            Assert.Equal("Kubernetes basics", movedNode.Title);
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""LogEntries"" WHERE ""NodeId"" = {nodeId};"));
            Assert.Equal(1, TestDatabase.Scalar($@"SELECT COUNT(*) FROM ""Actions"" WHERE ""KnowledgeNodeId"" = {nodeId};"));
        }

        [DbFact]
        public async Task Promote_TooLongNodeTitle_Returns400AndCreatesNoDomain()
        {
            var (_, nodeId) = SeedNode("Too long");
            int before = TestDatabase.Scalar(@"SELECT COUNT(*) FROM ""Domains"";");

            var res = await _fx.CreateClient().PostAsJsonAsync($"/kc/api/knowledge-nodes/{nodeId}/promote",
                new { KnowledgeNodeTitle = new string('x', 101) });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal(before, TestDatabase.Scalar(@"SELECT COUNT(*) FROM ""Domains"";"));
        }

        [DbFact]
        public async Task Promote_MissingNode_Returns404AndCreatesNoDomain()
        {
            int before = TestDatabase.Scalar(@"SELECT COUNT(*) FROM ""Domains"";");

            var res = await _fx.CreateClient().PostAsync("/kc/api/knowledge-nodes/999999/promote", null);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.Equal(before, TestDatabase.Scalar(@"SELECT COUNT(*) FROM ""Domains"";"));
        }

        [DbFact]
        public async Task Promote_ArchivedNode_Returns400()
        {
            var (_, nodeId) = SeedNode("Old stuff", archived: true);

            var res = await _fx.CreateClient().PostAsync($"/kc/api/knowledge-nodes/{nodeId}/promote", null);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
    }
}
