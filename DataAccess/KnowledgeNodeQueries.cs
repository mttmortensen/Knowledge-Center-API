using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.DataAccess
{
    public static class KnowledgeNodeQueries
    {
        public static readonly string InsertNode = @"
            INSERT INTO KnowledgeNodes 
                    (Title, DomainId, NodeType, Description, ConfidenceLevel, Status, CreatedAt, LastUpdated)
            VALUES 
                    (@Title, @DomainId, @NodeType, @Description, @ConfidenceLevel, @Status, @CreatedAt, @LastUpdated);
        ";

        public static readonly string SelectAllNodes = @"
            SELECT * FROM KnowledgeNodes;
        ";

        public static readonly string SelectNodeById = @"
            SELECT * FROM KnowledgeNodes 
            WHERE Id = @Id;
        ";

        public static readonly string UpdateNode = @"
            UPDATE KnowledgeNodes 
            SET Title = @Title, 
                DomainId = @DomainId, 
                NodeType = @NodeType,
                Description = @Description, 
                ConfidenceLevel = @ConfidenceLevel, 
                Status = @Status, 
                LastUpdated = @LastUpdated
            WHERE Id = @Id;
        ";

        public static readonly string DeleteNode = @"
            DELETE FROM KnowledgeNodes 
            WHERE Id = @Id;
        ";  
    }
}
