using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.DataAccess
{
    public class LogEntryQueries
    {
        public static readonly string InsertLogEntry = @"
            INSERT INTO LogEntries 
                    (NodeId, EntryDate, Content, TagId, ContributesToProgress)
            VALUES 
                    (@NodeId, @EntryDate, @Content, @TagId, @ContributesToProgress);
        ";

        public static readonly string GetLogsByNodeId = @"
            SELECT * FROM LogEntries 
            WHERE NodeId = @NodeId
            ORDER BY EntryDate DESC;
        ";

        public static readonly string GetAllLogs = @"
            SELECT * FROM LogEntries 
            ORDER BY EntryDate DESC;
        ";


        // This JOIN-based query includes Tag info directly with each LogEntry for better performance and fewer DB calls.
        // I handled KNs and Logs separately in C# earlier to test myself with multi-step composition and DTO mapping.
        // Here, I prioritized efficiency over modularity to keep things simple and fast for this use case.
        public static readonly string GetAllLogsWithTags = @"
            SELECT 
                l.LogId,
                l.NodeId,
                l.EntryDate,
                l.Content,
                l.ContributesToProgress,
                l.TagId,
                t.Name AS TagName
            FROM 
                LogEntries l
            LEFT JOIN 
                Tags t ON l.TagId = t.TagId;
        ";


        public static readonly string GetLogByLogId = @"
            SELECT * FROM LogEntries 
            WHERE LogId = @LogId;
        ";

        public static readonly string DeleteAllLogsByNodeId = @"
            DELETE FROM LogEntries 
            WHERE NodeId = @NodeId;
        ";
    }
}
