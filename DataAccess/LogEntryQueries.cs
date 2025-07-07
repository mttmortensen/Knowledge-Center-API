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
