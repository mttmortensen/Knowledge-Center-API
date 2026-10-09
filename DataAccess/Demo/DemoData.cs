using Knowledge_Center_API.Models.Actions;
using Knowledge_Center_API.Models.Domains;
using Knowledge_Center_API.Models.KnowledgeNodes;
using Knowledge_Center_API.Models.Learn;
using Knowledge_Center_API.Models.LogEntries;
using Knowledge_Center_API.Models.TagEntries;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.DataAccess.Demo
{
    public static class DemoData
    {
        public static readonly List<Domain> Domains;
        public static readonly List<KnowledgeNode> KnowledgeNodes;
        public static readonly List<LogEntry> LogEntries;
        public static readonly List<Tags> Tags;
        public static readonly List<ActionItem> Actions;
        public static readonly List<LearnSession> LearnSessions;
        public static readonly List<LearnEntry> LearnEntries;

        static DemoData()
        {
            var now = AppClock.Now;

            string[] domainNames =
            {
                "Distributed Systems", "Machine Learning", "Web Security",
                "Compilers & Languages", "Databases", "DevOps & Infrastructure"
            };

            Domains = domainNames.Select((name, i) =>
            {
                bool archived = i == domainNames.Length - 1;
                var createdAt = now.AddDays(-200 + i * 15);

                return new Domain
                {
                    DomainId = i + 1,
                    DomainName = name,
                    DomainDescription = $"Notes and progress tracking for {name}.",
                    DomainStatus = archived ? "Inactive" : "Active",
                    CreatedAt = createdAt,
                    LastUsed = now.AddDays(-i * 3),
                    LastUpdated = now.AddDays(-i * 2),
                    IsArchived = archived,
                    ArchivedAt = archived ? now.AddDays(-10) : (DateTime?)null
                };
            }).ToList();

            Tags = new[]
            {
                "research", "reading", "practice", "review", "blocked", "milestone",
                "reference", "experiment", "tooling", "interview-prep",
                "certification", "side-project", "mentoring", "writing"
            }.Select((name, i) => new Tags { TagId = i + 1, Name = name }).ToList();

            string[] nodeTopics =
            {
                "Fundamentals", "Advanced Patterns", "Common Pitfalls", "Case Study",
                "Best Practices", "Architecture Overview", "Performance Tuning", "Troubleshooting Guide"
            };
            string[] nodeTypes = { "Concept", "Project" };
            string[] nodeStatuses = { "Exploring", "Learning", "Mastered" };

            KnowledgeNodes = new List<KnowledgeNode>();
            int nodeId = 1;
            foreach (var domain in Domains)
            {
                for (int t = 0; t < 4; t++)
                {
                    string topic = nodeTopics[(t + domain.DomainId) % nodeTopics.Length];
                    var createdAt = domain.CreatedAt.AddDays(5 + t * 12);
                    bool archived = domain.IsArchived && t == 0;

                    KnowledgeNodes.Add(new KnowledgeNode
                    {
                        Id = nodeId,
                        DomainId = domain.DomainId,
                        Title = $"{domain.DomainName}: {topic}",
                        NodeType = nodeTypes[nodeId % nodeTypes.Length],
                        Description = $"Notes covering {topic.ToLower()} for {domain.DomainName}.",
                        ConfidenceLevel = (nodeId % 5) + 1,
                        Status = nodeStatuses[nodeId % nodeStatuses.Length],
                        CreatedAt = createdAt,
                        LastUpdated = createdAt.AddDays(3 + (nodeId % 10)),
                        IsArchived = archived,
                        ArchivedAt = archived ? createdAt.AddDays(30) : (DateTime?)null
                    });

                    nodeId++;
                }
            }

            string[] logTemplates =
            {
                "Explored {0} in more depth today.",
                "Documented findings related to {0}.",
                "Hit a roadblock with {0} — need to revisit.",
                "Made solid progress on {0}.",
                "Refined my understanding of {0}.",
                "Paired with a colleague on {0}.",
                "Read a reference article about {0}.",
                "Wrote a summary of {0} for future reference."
            };

            LogEntries = new List<LogEntry>();
            int logId = 1;
            foreach (var node in KnowledgeNodes)
            {
                int entryCount = 2 + (node.Id % 5);
                for (int e = 0; e < entryCount; e++)
                {
                    var entryDate = node.CreatedAt.AddDays(2 + e * 4);
                    bool hasTitle = logId % 3 != 0;
                    string template = logTemplates[(node.Id + e) % logTemplates.Length];

                    var tags = new List<Tags> { Tags[logId % Tags.Count] };
                    if (logId % 4 == 0)
                        tags.Add(Tags[(logId + 3) % Tags.Count]);

                    LogEntries.Add(new LogEntry
                    {
                        LogId = logId,
                        NodeId = node.Id,
                        EntryDate = entryDate,
                        Title = hasTitle ? $"Session {e + 1}: {node.Title}" : null,
                        Content = string.Format(template, node.Title),
                        Tags = tags
                    });

                    logId++;
                }
            }

            string[] actionTemplates =
            {
                "Review source material for {0}",
                "Write summary notes for {0}",
                "Apply {0} to a real project",
                "Schedule a follow-up session on {0}",
                "Find a mentor to discuss {0}",
                "Build a small demo covering {0}"
            };

            Actions = new List<ActionItem>();
            int actionId = 1;
            foreach (var node in KnowledgeNodes)
            {
                int actionCount = 1 + (node.Id % 3);
                for (int a = 0; a < actionCount; a++)
                {
                    var createdAt = node.CreatedAt.AddDays(1 + a * 5);
                    bool completed = actionId % 5 < 2;
                    string template = actionTemplates[(node.Id + a) % actionTemplates.Length];

                    Actions.Add(new ActionItem
                    {
                        Id = actionId,
                        KnowledgeNodeId = node.Id,
                        ActionText = string.Format(template, node.Title),
                        Status = completed ? "Completed" : "Open",
                        CreatedAt = createdAt,
                        CompletedAt = completed ? createdAt.AddDays(3 + (actionId % 7)) : (DateTime?)null
                    });

                    actionId++;
                }
            }

            // One Learn session filed under the first Databases node, showing each
            // entry type: note, question, code, follow-up and an open question.
            var learnNode = KnowledgeNodes.First(n => n.DomainId == 5);
            var learnStart = now.AddDays(-3);

            LearnSessions = new List<LearnSession>
            {
                new LearnSession
                {
                    SessionId = 1,
                    Title = "Postgres indexing basics",
                    Topic = "Databases",
                    NodeId = learnNode.Id,
                    NodeTitle = learnNode.Title,
                    CreatedAt = learnStart,
                    UpdatedAt = learnStart.AddHours(2)
                }
            };

            string[] learnSources =
            {
                "## B-tree indexes\nPostgres builds a **B-tree** index by default with `CREATE INDEX`. B-trees handle equality and range comparisons (`=`, `<`, `>`, `BETWEEN`).",
                "?? Does a composite index on `(a, b)` help a query that only filters on `b`?",
                "```sql\nCREATE INDEX idx_orders_customer_created\n    ON orders (customer_id, created_at);\n```",
                "@answers(2) Usually not much. A multicolumn B-tree is most efficient when the query constrains the *leading* column; filtering only on `b` means scanning the whole index.",
                "?? When is a partial index (`CREATE INDEX ... WHERE ...`) worth it over a full one?",
                "Next: run `EXPLAIN ANALYZE` on a query that filters only on `created_at` and compare the plan with one that filters on `customer_id`."
            };

            LearnEntries = learnSources.Select((source, i) => new LearnEntry
            {
                EntryId = i + 1,
                SessionId = 1,
                Position = (i + 1) * LearnService.PositionGap,
                Source = source,
                CreatedAt = learnStart.AddMinutes(i * 20),
                UpdatedAt = learnStart.AddMinutes(i * 20)
            }).ToList();

            LearnSourceParser.Annotate(LearnEntries);
            LearnSessions[0].EntryCount = LearnEntries.Count;
            LearnSessions[0].OpenQuestionCount = LearnEntries.Count(e => e.QuestionStatus == "open");
        }
    }
}
