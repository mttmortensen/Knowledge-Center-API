using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.DataAccess
{
    public static class DomainQueries
    {
        public static readonly string InsertDomain = @"
            INSERT INTO ""Domains""
                (""DomainName"", ""DomainDescription"", ""DomainStatus"", ""CreatedAt"", ""LastUsed"")
            VALUES
                (@DomainName, @DomainDescription, @DomainStatus, @CreatedAt, @LastUsed)
            RETURNING ""DomainId"";
        ";

        public static readonly string GetAllDomains = @"
            SELECT * FROM ""Domains""
            WHERE ""IsArchived"" = FALSE;
        ";

        public static readonly string GetAllDomainsIncludingArchived = @"
            SELECT * FROM ""Domains"";
        ";

        public static readonly string GetDomainById = @"
            SELECT * FROM ""Domains""
            WHERE
                ""DomainId"" = @DomainId;
        ";

        public static readonly string UpdateDomain = @"
            UPDATE ""Domains""
            SET
                ""DomainName"" = @DomainName,
                ""DomainDescription"" = @DomainDescription,
                ""DomainStatus"" = @DomainStatus,
                ""LastUsed"" = @LastUsed
            WHERE
                ""DomainId"" = @DomainId;
        ";

        // Removes the domain together with its knowledge nodes and their actions and
        // log entries. The foreign keys don't cascade, so the children go first; as one
        // statement it's atomic, and the FK checks only run once it has finished.
        // Log entry tags cascade on their own; Learn sessions just lose their node link.
        public static readonly string DeleteDomain = @"
            WITH ""DomainNodes"" AS (
                SELECT ""Id"" FROM ""KnowledgeNodes"" WHERE ""DomainId"" = @DomainId
            ),
            ""DeletedActions"" AS (
                DELETE FROM ""Actions""
                WHERE ""KnowledgeNodeId"" IN (SELECT ""Id"" FROM ""DomainNodes"")
            ),
            ""DeletedLogs"" AS (
                DELETE FROM ""LogEntries""
                WHERE ""NodeId"" IN (SELECT ""Id"" FROM ""DomainNodes"")
            ),
            ""DeletedNodes"" AS (
                DELETE FROM ""KnowledgeNodes""
                WHERE ""DomainId"" = @DomainId
            )
            DELETE FROM ""Domains""
            WHERE
                ""DomainId"" = @DomainId;
        ";

        public static readonly string ArchiveDomain = @"
            UPDATE ""Domains""
            SET ""IsArchived"" = TRUE, ""ArchivedAt"" = @ArchivedAt
            WHERE ""DomainId"" = @DomainId;
        ";

        public static readonly string UnarchiveDomain = @"
            UPDATE ""Domains""
            SET ""IsArchived"" = FALSE, ""ArchivedAt"" = NULL
            WHERE ""DomainId"" = @DomainId;
        ";

    }
}
