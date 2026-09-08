using System;

namespace Knowledge_Center_API.Models.Actions
{
    public class ActionItem
    {
        public int Id { get; set; }
        public int KnowledgeNodeId { get; set; }
        public string ActionText { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
