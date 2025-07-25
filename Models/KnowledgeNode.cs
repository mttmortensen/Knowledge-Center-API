﻿namespace Knowledge_Center_API.Models
{
    public class KnowledgeNode
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int DomainId { get; set; }
        public string NodeType { get; set; } 
        public string Description { get; set; }
        public int ConfidenceLevel { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }


    }
}
