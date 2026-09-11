using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Actions;
using Knowledge_Center_API.Models.Stats;
using Knowledge_Center_API.Services.Validation;
using Npgsql;
using NpgsqlTypes;

namespace Knowledge_Center_API.Services.Core
{
    public class ActionService
    {
        private readonly Database _database;
        private readonly KnowledgeNodeService _knService;

        public ActionService(Database database, KnowledgeNodeService knService)
        {
            _database = database;
            _knService = knService;
        }

        /* ===================== CRUD ===================== */

        // === CREATE ===
        public bool CreateActionItem(ActionItem action)
        {
            // Validate Inputs Using Validators
            FieldValidator.ValidateRequiredString(action.ActionText, "ActionText", 500);
            FieldValidator.ValidateId(action.KnowledgeNodeId, "KnowledgeNode ID");

            if (!_knService.KnowledgeNodeExists(action.KnowledgeNodeId))
                throw new ArgumentException($"KnowledgeNode with ID {action.KnowledgeNodeId} not found.");

            // Status is always server-controlled on create
            action.Status = "Open";
            action.CreatedAt = DateTime.Now;
            action.CompletedAt = null;

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@KnowledgeNodeId", NpgsqlDbType.Integer) { Value = action.KnowledgeNodeId },
                new NpgsqlParameter("@ActionText", NpgsqlDbType.Varchar, 500) { Value = action.ActionText },
                new NpgsqlParameter("@Status", NpgsqlDbType.Varchar, 20) { Value = action.Status },
                new NpgsqlParameter("@CreatedAt", NpgsqlDbType.Timestamp) { Value = action.CreatedAt }
            };

            int newActionId = _database.ExecuteScalar<int>(ActionQueries.InsertAction, parameters);
            if (newActionId <= 0)
                return false;

            action.Id = newActionId;
            return true;
        }

        // === READ ===
        public List<ActionItem> GetOpenActionsForNode(int nodeId)
        {
            FieldValidator.ValidateId(nodeId, "KnowledgeNode ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@KnowledgeNodeId", nodeId)
            };

            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetOpenActionsByNodeId, parameters);
            return rawDBResults.Select(ConvertDBRowToActionItem).ToList();
        }

        public List<ActionItem> GetCompletedActionsForNode(int nodeId)
        {
            FieldValidator.ValidateId(nodeId, "KnowledgeNode ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@KnowledgeNodeId", nodeId)
            };

            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetCompletedActionsByNodeId, parameters);
            return rawDBResults.Select(ConvertDBRowToActionItem).ToList();
        }

        public ActionItem GetActionById(int id)
        {
            FieldValidator.ValidateId(id, "Action ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", id)
            };

            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetActionById, parameters);
            if (rawDBResults.Count == 0)
                return null;

            return ConvertDBRowToActionItem(rawDBResults[0]);
        }

        public List<ActionItem> GetAllOpenActions()
        {
            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetAllOpenActions, null);
            return rawDBResults.Select(ConvertDBRowToActionItem).ToList();
        }

        public List<ActionItem> GetAllCompletedActions()
        {
            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetAllCompletedActions, null);
            return rawDBResults.Select(ConvertDBRowToActionItem).ToList();
        }

        public List<ActionOpenCountDto> GetOpenActionCountsByNode()
        {
            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetOpenActionCountsByNode, null);

            return rawDBResults.Select(row => new ActionOpenCountDto
            {
                KnowledgeNodeId = Convert.ToInt32(row["KnowledgeNodeId"]),
                OpenCount = Convert.ToInt32(row["OpenCount"])
            })
            .ToList();
        }

        // === UPDATE ===
        public bool UpdateActionText(int id, ActionItemUpdateDto dto)
        {
            var existing = GetActionById(id);
            if (existing == null)
                return false;

            string newText = !string.IsNullOrWhiteSpace(dto.ActionText) ? dto.ActionText : existing.ActionText;
            FieldValidator.ValidateRequiredString(newText, "ActionText", 500);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", NpgsqlDbType.Integer) { Value = id },
                new NpgsqlParameter("@ActionText", NpgsqlDbType.Varchar, 500) { Value = newText }
            };

            int result = _database.ExecuteNonQuery(ActionQueries.UpdateActionText, parameters);
            return result > 0;
        }

        public bool CompleteAction(int id)
        {
            var existing = GetActionById(id);
            if (existing == null)
                return false;

            // Idempotent: already completed, nothing to do
            if (existing.Status == "Completed")
                return true;

            return SetActionStatus(id, "Completed", DateTime.Now);
        }

        public bool ReopenAction(int id)
        {
            var existing = GetActionById(id);
            if (existing == null)
                return false;

            // Idempotent: already open, nothing to do
            if (existing.Status == "Open")
                return true;

            return SetActionStatus(id, "Open", null);
        }

        private bool SetActionStatus(int id, string status, DateTime? completedAt)
        {
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", NpgsqlDbType.Integer) { Value = id },
                new NpgsqlParameter("@Status", NpgsqlDbType.Varchar, 20) { Value = status },
                new NpgsqlParameter("@CompletedAt", NpgsqlDbType.Timestamp)
                {
                    Value = completedAt.HasValue ? completedAt.Value : DBNull.Value
                }
            };

            int result = _database.ExecuteNonQuery(ActionQueries.UpdateActionStatus, parameters);
            return result > 0;
        }

        // === DELETE ===
        public bool DeleteActionItem(int id)
        {
            FieldValidator.ValidateId(id, "Action ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", id)
            };

            int result = _database.ExecuteNonQuery(ActionQueries.DeleteAction, parameters);
            return result > 0;
        }

        public bool DeleteAllActionsByNodeId(int nodeId)
        {
            FieldValidator.ValidateId(nodeId, "KnowledgeNode ID");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@KnowledgeNodeId", NpgsqlDbType.Integer) { Value = nodeId }
            };

            // A node with zero actions is a valid, successful outcome here (nothing to
            // delete), not a failure — unlike DeleteActionItem, this isn't a lookup-by-id.
            _database.ExecuteNonQuery(ActionQueries.DeleteAllActionsByNodeId, parameters);
            return true;
        }

        /* ===================== STATS ===================== */

        // Most recently created actions across every knowledge node, with the
        // owning node's title attached so dashboards don't need a second round trip.
        public List<RecentActionDto> GetRecentActions(int limit)
        {
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Limit", NpgsqlDbType.Integer) { Value = limit }
            };

            var rawDBResults = _database.ExecuteQuery(ActionQueries.GetRecentActions, parameters);

            return rawDBResults.Select(row => new RecentActionDto
            {
                Id = Convert.ToInt32(row["Id"]),
                KnowledgeNodeId = Convert.ToInt32(row["KnowledgeNodeId"]),
                KnowledgeNodeTitle = row["KnowledgeNodeTitle"].ToString(),
                ActionText = row["ActionText"].ToString(),
                Status = row["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
                CompletedAt = row["CompletedAt"] == null || row["CompletedAt"] == DBNull.Value
                    ? (DateTime?)null
                    : Convert.ToDateTime(row["CompletedAt"])
            }).ToList();
        }

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */
        private ActionItem ConvertDBRowToActionItem(Dictionary<string, object> rawDBRow)
        {
            return new ActionItem
            {
                Id = Convert.ToInt32(rawDBRow["Id"]),
                KnowledgeNodeId = Convert.ToInt32(rawDBRow["KnowledgeNodeId"]),
                ActionText = rawDBRow["ActionText"].ToString(),
                Status = rawDBRow["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                CompletedAt = rawDBRow["CompletedAt"] == null || rawDBRow["CompletedAt"] == DBNull.Value
                    ? (DateTime?)null
                    : Convert.ToDateTime(rawDBRow["CompletedAt"])
            };
        }
    }
}
