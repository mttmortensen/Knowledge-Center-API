namespace Knowledge_Center_API.DataAccess
{
    public static class ActionQueries
    {
        public static readonly string InsertAction = @"
            INSERT INTO ""Actions""
                (""KnowledgeNodeId"", ""ActionText"", ""Status"", ""CreatedAt"")
            VALUES
                (@KnowledgeNodeId, @ActionText, @Status, @CreatedAt)
            RETURNING ""Id"";
        ";

        public static readonly string GetActionById = @"
            SELECT * FROM ""Actions""
            WHERE ""Id"" = @Id;
        ";

        public static readonly string GetOpenActionsByNodeId = @"
            SELECT * FROM ""Actions""
            WHERE ""KnowledgeNodeId"" = @KnowledgeNodeId AND ""Status"" = 'Open'
            ORDER BY ""CreatedAt"" DESC;
        ";

        public static readonly string GetCompletedActionsByNodeId = @"
            SELECT * FROM ""Actions""
            WHERE ""KnowledgeNodeId"" = @KnowledgeNodeId AND ""Status"" = 'Completed'
            ORDER BY ""CompletedAt"" DESC;
        ";

        public static readonly string GetAllOpenActions = @"
            SELECT * FROM ""Actions""
            WHERE ""Status"" = 'Open'
            ORDER BY ""KnowledgeNodeId"", ""CreatedAt"" DESC;
        ";

        public static readonly string GetAllCompletedActions = @"
            SELECT * FROM ""Actions""
            WHERE ""Status"" = 'Completed'
            ORDER BY ""KnowledgeNodeId"", ""CompletedAt"" DESC;
        ";

        public static readonly string GetOpenActionCountsByNode = @"
            SELECT ""KnowledgeNodeId"", COUNT(*) AS ""OpenCount""
            FROM ""Actions""
            WHERE ""Status"" = 'Open'
            GROUP BY ""KnowledgeNodeId"";
        ";

        public static readonly string UpdateActionText = @"
            UPDATE ""Actions""
            SET ""ActionText"" = @ActionText
            WHERE ""Id"" = @Id;
        ";

        public static readonly string UpdateActionStatus = @"
            UPDATE ""Actions""
            SET ""Status"" = @Status, ""CompletedAt"" = @CompletedAt
            WHERE ""Id"" = @Id;
        ";

        public static readonly string DeleteAction = @"
            DELETE FROM ""Actions""
            WHERE ""Id"" = @Id;
        ";

        public static readonly string DeleteAllActionsByNodeId = @"
            DELETE FROM ""Actions""
            WHERE ""KnowledgeNodeId"" = @KnowledgeNodeId;
        ";

        public static readonly string GetRecentActions = @"
            SELECT a.*, kn.""Title"" AS ""KnowledgeNodeTitle""
            FROM ""Actions"" a
            INNER JOIN ""KnowledgeNodes"" kn ON kn.""Id"" = a.""KnowledgeNodeId""
            ORDER BY a.""CreatedAt"" DESC
            LIMIT @Limit;
        ";
    }
}
