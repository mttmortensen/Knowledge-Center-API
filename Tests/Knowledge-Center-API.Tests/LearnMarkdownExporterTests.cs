using Knowledge_Center_API.Models.Learn;
using Knowledge_Center_API.Models.LogEntries;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Tests
{
    public class LearnMarkdownExporterTests
    {
        private static LearnSessionDetailsDto Session(params (int id, long pos, string source)[] entries) => new()
        {
            SessionId = 1,
            Title = "Indexes",
            Topic = "Databases",
            CreatedAt = new DateTime(2026, 10, 1),
            Entries = entries.Select(e => new LearnEntry
            {
                EntryId = e.id,
                SessionId = 1,
                Position = e.pos,
                Source = e.source,
                CreatedAt = new DateTime(2026, 10, 1, 9, 0, 0)
            }).ToList()
        };

        [Fact]
        public void Build_WritesEntriesInPositionOrderWithAnchors()
        {
            var session = Session((2, 300, "third"), (1, 100, "?? first"), (3, 200, "@answers(1) second"));

            string md = LearnMarkdownExporter.Build(session, _ => null);

            Assert.StartsWith("# Indexes", md);
            Assert.Contains("Topic: Databases", md);
            int first = md.IndexOf("?? first");
            int second = md.IndexOf("@answers(1) second");
            int third = md.IndexOf("\nthird");
            Assert.True(first < second && second < third);
            Assert.Contains("<!-- entry 1 · question", md);
            Assert.Contains("<!-- entry 3 · followup", md);
            Assert.DoesNotContain("\r", md);
        }

        [Fact]
        public void Build_ExpandsLogReferences()
        {
            var session = Session((1, 100, "@log(5)"), (2, 200, "@log(6)"));
            var log = new LogEntry { LogId = 5, Title = "Read the docs", Content = "Learned about GIN.", EntryDate = new DateTime(2026, 9, 30) };

            string md = LearnMarkdownExporter.Build(session, id => id == 5 ? log : null);

            Assert.Contains("**Read the docs**", md);
            Assert.Contains("Learned about GIN.", md);
            Assert.Contains("_Log #6 was deleted._", md);
            Assert.DoesNotContain("\n@log(5)", md);
        }
    }
}
