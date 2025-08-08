using Knowledge_Center_API.Models.Domains;
using Knowledge_Center_API.Models.KnowledgeNodes;
using Knowledge_Center_API.Models.LogEntries;
using Knowledge_Center_API.Models.Tags;

namespace Knowledge_Center_API.DataAccess.Demo
{
    public static class DemoData
    {
        public static List<Domain> Domains = new()
    {
        new Domain { DomainId = 1, DomainName = "Demo Domain", DomainDescription = "This is a fake domain loaded in demo mode." }
    };

        public static List<KnowledgeNode> KnowledgeNodes = new()
    {
        new KnowledgeNode { Id = 1, DomainId = 1, Title = "Welcome Node", Description = "This is a demo node!", CreatedAt = DateTime.UtcNow }
    };

        public static List<LogEntry> LogEntries = new()
    {
        new LogEntry { LogId = 1, NodeId = 1, Content = "This is a sample log entry.", EntryDate = DateTime.UtcNow }
    };

        public static List<Tags> Tags = new()
    {
        new Tags { TagId = 1, Name = "demo" }
    };
    }
}
