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
            INSERT INTO ""KnowledgeNodes""
                    (""Title"", ""DomainId"", ""NodeType"", ""Description"", ""ConfidenceLevel"", ""Status"", ""CreatedAt"", ""LastUpdated"")
            VALUES
                    (@Title, @DomainId, @NodeType, @Description, @ConfidenceLevel, @Status, @CreatedAt, @LastUpdated)
            RETURNING ""Id"";
        ";

        public static readonly string GetAllKnowledgeNodes = @"
            SELECT * FROM ""KnowledgeNodes""
            WHERE ""IsArchived"" = FALSE;
        ";

        public static readonly string GetAllKnowledgeNodesIncludingArchived = @"
            SELECT * FROM ""KnowledgeNodes"";
        ";

        public static readonly string GetKnowledgeNodesByDomainId = @"
            SELECT * FROM ""KnowledgeNodes""
            WHERE ""DomainId"" = @DomainId
            ORDER BY ""LastUpdated"" DESC;
        ";

        public static readonly string GetKnowledgeNodeById = @"
            SELECT * FROM ""KnowledgeNodes""
            WHERE ""Id"" = @Id;
        ";

        public static readonly string UpdateKnowledgeNode = @"
            UPDATE ""KnowledgeNodes""
            SET ""Title"" = @Title,
                ""DomainId"" = @DomainId,
                ""NodeType"" = @NodeType,
                ""Description"" = @Description,
                ""ConfidenceLevel"" = @ConfidenceLevel,
                ""Status"" = @Status,
                ""LastUpdated"" = @LastUpdated
            WHERE ""Id"" = @Id;
        ";

        public static readonly string DeleteKnowledgeNode = @"
            DELETE FROM ""KnowledgeNodes""
            WHERE ""Id"" = @Id;
        ";

        public static readonly string ArchiveKnowledgeNode = @"
            UPDATE ""KnowledgeNodes""
            SET ""IsArchived"" = TRUE, ""ArchivedAt"" = @ArchivedAt
            WHERE ""Id"" = @Id;
        ";

        public static readonly string UnarchiveKnowledgeNode = @"
            UPDATE ""KnowledgeNodes""
            SET ""IsArchived"" = FALSE, ""ArchivedAt"" = NULL
            WHERE ""Id"" = @Id;
        ";

        public static readonly string ArchiveKnowledgeNodesByDomainId = @"
            UPDATE ""KnowledgeNodes""
            SET ""IsArchived"" = TRUE, ""ArchivedAt"" = @ArchivedAt
            WHERE ""DomainId"" = @DomainId;
        ";

        public static readonly string UnarchiveKnowledgeNodesByDomainId = @"
            UPDATE ""KnowledgeNodes""
            SET ""IsArchived"" = FALSE, ""ArchivedAt"" = NULL
            WHERE ""DomainId"" = @DomainId;
        ";
    }
}
