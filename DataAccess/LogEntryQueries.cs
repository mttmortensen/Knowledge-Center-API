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
                (NodeId, EntryDate, Content, ContributesToProgress, ChatURL)
            OUTPUT INSERTED.LogId
            VALUES 
                (@NodeId, @EntryDate, @Content, @ContributesToProgress, @ChatURL);
        ";

        public static readonly string InsertLogTagRelation = @"
            INSERT INTO LogEntryTags (LogId, TagId)
            VALUES (@LogId, @TagId);
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


        // Removing GetAllLogsWithTags query since now 
        // We are building out the logs and tags in the service layer.
        public static readonly string GetAllLogsWithoutTags = @"
            SELECT LogId, NodeId, EntryDate, Content, ContributesToProgress, ChatURL
            FROM LogEntries
            ORDER BY EntryDate DESC;
        ";

        public static readonly string GetAllLogTagRelations = @"
            SELECT lt.LogId, t.TagId, t.Name AS TagName
            FROM LogEntryTags lt
            INNER JOIN Tags t ON lt.TagId = t.TagId;
        ";


        public static readonly string GetLogByLogId = @"
            SELECT ltr.LogId, le.NodeId, le.EntryDate, le.Content, le.ContributesToProgress, le.ChatURL,
                   ltr.TagId, t.Name AS TagName
            FROM LogEntries le
            LEFT JOIN LogTagRelations ltr ON le.LogId = ltr.LogId
            LEFT JOIN Tags t ON ltr.TagId = t.TagId
            WHERE le.LogId = @LogId;
        ";


        public static readonly string GetLogByIdWithoutTags = @"
            SELECT LogId, NodeId, EntryDate, Content, ContributesToProgress, ChatURL
            FROM LogEntries
            WHERE LogId = @LogId;
        ";

        public static readonly string GetLogTagRelationsByLogId = @"
            SELECT lt.LogId,
                   t.TagId,
                   t.Name AS TagName
            FROM LogEntryTags lt
            INNER JOIN Tags t
                ON lt.TagId = t.TagId
            WHERE lt.LogId = @LogId;
        ";

        public static readonly string UpdateChatURLByLogId = @"
            UPDATE LogEntries
            SET ChatURL = @ChatURL
            WHERE LogId = @LogId;
        ";


        public static readonly string DeleteAllLogsByNodeId = @"
            DELETE FROM LogEntries 
            WHERE NodeId = @NodeId;
        ";

        public static readonly string DeleteLogTagRelations = @"
            DELETE FROM LogEntryTags
            WHERE LogId = @LogId AND TagId = @TagId;
        ";
    }
}
