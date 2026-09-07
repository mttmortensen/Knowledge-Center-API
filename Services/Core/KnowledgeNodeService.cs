using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.KnowledgeNodes;
using Knowledge_Center_API.Models.LogEntries;
using Knowledge_Center_API.Services.Validation;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.Services.Core
{
    public class KnowledgeNodeService
    {
        private readonly Database _database;
        private readonly LogEntryService _lgservice;

        public KnowledgeNodeService(Database database, LogEntryService lgservice)
        {
            _database = database;
            _lgservice = lgservice;
        }

        /* ===================== CRUD ===================== */

        // === CREATE ===
        public bool CreateKnowledgeNode(KnowledgeNode node)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateRequiredString(node.Title, "Title", 100);
            FieldValidator.ValidateRequiredString(node.Description, "Description", 500);

            FieldValidator.ValidateEnumValue(node.NodeType, "NodeType", new() { "Concept", "Project" });
            FieldValidator.ValidateEnumValue(node.Status, "Status", new() { "Exploring", "Learning", "Mastered" });


            // Set timestamps
            DateTime now = DateTime.Now;
            node.CreatedAt = now;
            node.LastUpdated = now;

            // Build SQL Parameters
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Title", NpgsqlDbType.Varchar, 100) { Value = node.Title },
                new NpgsqlParameter("@DomainId", NpgsqlDbType.Integer) { Value = node.DomainId },
                new NpgsqlParameter("@Description", NpgsqlDbType.Varchar, 500) { Value = node.Description },
                new NpgsqlParameter("@ConfidenceLevel", NpgsqlDbType.Integer) { Value = node.ConfidenceLevel },
                new NpgsqlParameter("@Status", NpgsqlDbType.Varchar, 20) { Value = node.Status },
                new NpgsqlParameter("@CreatedAt", NpgsqlDbType.Timestamp) { Value = node.CreatedAt },
                new NpgsqlParameter("@LastUpdated", NpgsqlDbType.Timestamp) { Value = node.LastUpdated },
                new NpgsqlParameter("@NodeType", NpgsqlDbType.Varchar, 20) { Value = node.NodeType }
            };

            // Run the INSERT query and capture the DB-generated id
            int newNodeId = _database.ExecuteScalar<int>(KnowledgeNodeQueries.InsertNode, parameters);
            if (newNodeId <= 0)
                return false;

            node.Id = newNodeId;
            return true;
        }

        // === READ ===
        public List<KnowledgeNodeListDto> GetAllKnowledgeNodes()
        {
            // SELECT Query + Parameters to retrieve all KnowledgeNodes and map results into KnowledgeNode objects
            List<KnowledgeNodeListDto> nodes = new List<KnowledgeNodeListDto>();

            var rawDBResults = _database.ExecuteQuery(KnowledgeNodeQueries.GetAllKnowledgeNodes, null);

            foreach (var rawDBRow in rawDBResults)
            {
                nodes.Add(ConvertDBRowToKNBaseObj(rawDBRow));
            }

            return nodes;
        }

        public KnowledgeNodeDetailsWithLogsDto GetKnowledgeNodeWithLogsById(int id)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateId(id, "KnowledgeNode ID");

            // Fetch Raw KN Data
            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", id )
            };

            var rawDBResults = _database.ExecuteQuery(KnowledgeNodeQueries.GetKnowledgeNodeById, parameters);

            if (rawDBResults.Count == 0)
            {
                return null; // No node found with the given ID
            }

            // Convert it into the KNwLogs Dto which we'll return 
            // but first need to fetch logs and add them to this dto
            KnowledgeNodeDetailsWithLogsDto nodeDto = ConvertDBRowToKNWithLogsDto(rawDBResults[0]);

            // Fetch Logs
            List<LogEntry> logs = _lgservice.GetLogsForKnowledgeNode(id);

            // Adding Logs to Dto
            nodeDto.Logs = logs.Select(log => new LogEntryDetailsInlineDto 
            {
                LogId = log.LogId,
                Content = log.Content,
                EntryDate = log.EntryDate,
                ContributesToProgress = log.ContributesToProgress

            })
            .ToList();

            return nodeDto;
        }

        // === UPDATE ===

        // This update method is what can allow to not have every field 
        // updated. This will return the Dto for KN which allows for the
        // '?' fields
        public bool UpdateKnowledgeNodeFromDto(int nodeId, KnowledgeNodeUpdateDto dto)
        {
            KnowledgeNodeDetailsWithLogsDto existingKNDto = GetKnowledgeNodeWithLogsById(nodeId);
            if (existingKNDto == null)
                return false;

            // Only update fields that were sent
            if (!string.IsNullOrWhiteSpace(dto.Title)) existingKNDto.Title = dto.Title;
            if (!string.IsNullOrWhiteSpace(dto.Description)) existingKNDto.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Status)) existingKNDto.Status = dto.Status;
            if (!string.IsNullOrWhiteSpace(dto.NodeType)) existingKNDto.NodeType = dto.NodeType;
            if (dto.ConfidenceLevel.HasValue) existingKNDto.ConfidenceLevel = dto.ConfidenceLevel.Value;
            if (dto.DomainId.HasValue) existingKNDto.DomainId = dto.DomainId.Value;

            existingKNDto.LastUpdated = DateTime.Now;


            // Manually map to base model before passing to UpdateKnowledgeNode
            KnowledgeNode node = new KnowledgeNode
            {
                Id = existingKNDto.Id,
                Title = existingKNDto.Title,
                DomainId = existingKNDto.DomainId,
                NodeType = existingKNDto.NodeType,
                Description = existingKNDto.Description,
                ConfidenceLevel = existingKNDto.ConfidenceLevel,
                Status = existingKNDto.Status,
                LastUpdated = existingKNDto.LastUpdated
            };

            return UpdateKnowledgeNode(node);
        }

        private bool UpdateKnowledgeNode(KnowledgeNode node)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateRequiredString(node.Title, "Title", 100);
            FieldValidator.ValidateRequiredString(node.Description, "Description", 500);

            FieldValidator.ValidateEnumValue(node.NodeType, "NodeType", new() { "Concept", "Project" });
            FieldValidator.ValidateEnumValue(node.Status, "Status", new() { "Exploring", "Learning", "Mastered" });

            // UPDATE Query + Parameters to update an existingKNDto KnowledgeNode by it's ID
            node.LastUpdated = DateTime.Now;

            // === Strictly Typed SQL Parameters ===
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", NpgsqlDbType.Integer) { Value = node.Id },
                new NpgsqlParameter("@Title", NpgsqlDbType.Varchar, 100) { Value = node.Title },
                new NpgsqlParameter("@DomainId", NpgsqlDbType.Integer) { Value = node.DomainId },
                new NpgsqlParameter("@NodeType", NpgsqlDbType.Varchar, 20) { Value = node.NodeType },
                new NpgsqlParameter("@Description", NpgsqlDbType.Varchar, 500) { Value = node.Description },
                new NpgsqlParameter("@ConfidenceLevel", NpgsqlDbType.Integer) { Value = node.ConfidenceLevel },
                new NpgsqlParameter("@Status", NpgsqlDbType.Varchar, 20) { Value = node.Status },
                new NpgsqlParameter("@LastUpdated", NpgsqlDbType.Timestamp) { Value = node.LastUpdated }
            };

            int result = _database.ExecuteNonQuery(KnowledgeNodeQueries.UpdateKnowledgeNode, parameters);

            // Return true to see if UPDATE was successful
            return result > 0;
        }

        // === DELETE ===
        public bool DeleteKnowledgeNode(int id)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateId(id, "KnowledgeNode ID");


            // DELETE Query + Parameters to delete a KnowledgeNode by ID
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Id", id)
            };

            int result = _database.ExecuteNonQuery(KnowledgeNodeQueries.DeleteKnowledgeNode, parameters);

            // Return true to see if DELETE was successful
            return result > 0;
        }

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */
        private KnowledgeNodeListDto ConvertDBRowToKNBaseObj(Dictionary<string, object> rawDBRow)
        {
            return new KnowledgeNodeListDto
            {
                Id = Convert.ToInt32(rawDBRow["Id"]),
                Title = rawDBRow["Title"].ToString(),
                DomainId = Convert.ToInt32(rawDBRow["DomainId"]),
                NodeType = rawDBRow["NodeType"].ToString(),
                Description = rawDBRow["Description"].ToString(),
                ConfidenceLevel = Convert.ToInt32(rawDBRow["ConfidenceLevel"]),
                Status = rawDBRow["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                LastUpdated = Convert.ToDateTime(rawDBRow["LastUpdated"]),
                Logs = new List<LogEntryListDto>()
            };
        }

        private KnowledgeNodeDetailsWithLogsDto ConvertDBRowToKNWithLogsDto(Dictionary<string, object> rawDBRow) 
        {
            return new KnowledgeNodeDetailsWithLogsDto
            {
                Id = Convert.ToInt32(rawDBRow["Id"]),
                Title = rawDBRow["Title"].ToString(),
                DomainId = Convert.ToInt32(rawDBRow["DomainId"]),
                NodeType = rawDBRow["NodeType"].ToString(),
                Description = rawDBRow["Description"].ToString(),
                ConfidenceLevel = Convert.ToInt32(rawDBRow["ConfidenceLevel"]),
                Status = rawDBRow["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                LastUpdated = Convert.ToDateTime(rawDBRow["LastUpdated"]),
                Logs = new List<LogEntryDetailsInlineDto>()
            };
        }
    }
}
