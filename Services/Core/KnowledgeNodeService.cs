using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models;
using Knowledge_Center_API.Models.DTOs;
using Knowledge_Center_API.Services.Validation;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.Services.Core
{
    public class KnowledgeNodeService
    {
        private readonly Database _database;

        public KnowledgeNodeService(Database database)
        {
            _database = database;
        }

        /* ===================== CRUD ===================== */

        // === CREATE ===
        public bool CreateNode(KnowledgeNode node)
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
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Title", SqlDbType.NVarChar, 100) { Value = node.Title },
                new SqlParameter("@DomainId", SqlDbType.Int) { Value = node.DomainId },
                new SqlParameter("@Description", SqlDbType.NVarChar, 500) { Value = node.Description },
                new SqlParameter("@ConfidenceLevel", SqlDbType.Int) { Value = node.ConfidenceLevel },
                new SqlParameter("@Status", SqlDbType.NVarChar, 20) { Value = node.Status },
                new SqlParameter("@CreatedAt", SqlDbType.DateTime) { Value = node.CreatedAt },
                new SqlParameter("@LastUpdated", SqlDbType.DateTime) { Value = node.LastUpdated },
                new SqlParameter("@NodeType", SqlDbType.NVarChar, 20) { Value = node.NodeType }
            };

            // Run the INSERT query
            int result = _database.ExecuteNonQuery(KnowledgeNodeQueries.InsertNode, parameters);

            // Return true to see if INSERT was successful
            return result > 0;
        }

        // === READ ===
        public List<KnowledgeNode> GetAllNodes()
        {
            // SELECT Query + Parameters to retrieve all KnowledgeNodes and map results into KnowledgeNode objects
            List<KnowledgeNode> nodes = new List<KnowledgeNode>();

            var rawDBResults = _database.ExecuteQuery(KnowledgeNodeQueries.SelectAllNodes, null);

            foreach (var rawDBRow in rawDBResults)
            {
                nodes.Add(ConvertDBRowToClassObj(rawDBRow));
            }

            return nodes;
        }

        public KnowledgeNode GetNodeById(int id)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateId(id, "KnowledgeNode ID");

            // SELECT Query + Parameters to retrieve a specific KnowledgeNode by ID and map result into a KnowledgeNode object
            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@Id", id )
            };

            var rawDBResults = _database.ExecuteQuery(KnowledgeNodeQueries.SelectNodeById, parameters);

            if (rawDBResults.Count == 0)
            {
                return null; // No node found with the given ID
            }

            return ConvertDBRowToClassObj(rawDBResults[0]);
        }

        // === UPDATE ===

        // This update method is what can allow to not have every field 
        // updated. This will return the Dto for KN which allows for the
        // '?' fields
        public bool UpdateNodeFromDto(int nodeId, KnowledgeNodeUpdateDto dto)
        {
            var existing = GetNodeById(nodeId);
            if (existing == null)
                return false;

            // Only update fields that were sent
            if (!string.IsNullOrWhiteSpace(dto.Title)) existing.Title = dto.Title;
            if (!string.IsNullOrWhiteSpace(dto.Description)) existing.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Status)) existing.Status = dto.Status;
            if (!string.IsNullOrWhiteSpace(dto.NodeType)) existing.NodeType = dto.NodeType;
            if (dto.ConfidenceLevel.HasValue) existing.ConfidenceLevel = dto.ConfidenceLevel.Value;
            if (dto.DomainId.HasValue) existing.DomainId = dto.DomainId.Value;

            existing.LastUpdated = DateTime.Now;

            return UpdateNode(existing);
        }


        public bool UpdateNode(KnowledgeNode node)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateRequiredString(node.Title, "Title", 100);
            FieldValidator.ValidateRequiredString(node.Description, "Description", 500);

            FieldValidator.ValidateEnumValue(node.NodeType, "NodeType", new() { "Concept", "Project" });
            FieldValidator.ValidateEnumValue(node.Status, "Status", new() { "Exploring", "Learning", "Mastered" });


            // UPDATE Query + Parameters to update an existing KnowledgeNode by it's ID
            node.LastUpdated = DateTime.Now;

            // === Strictly Typed SQL Parameters ===
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Id", SqlDbType.Int) { Value = node.Id },
                new SqlParameter("@Title", SqlDbType.NVarChar, 100) { Value = node.Title },
                new SqlParameter("@DomainId", SqlDbType.Int) { Value = node.DomainId },
                new SqlParameter("@NodeType", SqlDbType.NVarChar, 20) { Value = node.NodeType },
                new SqlParameter("@Description", SqlDbType.NVarChar, 500) { Value = node.Description },
                new SqlParameter("@ConfidenceLevel", SqlDbType.Int) { Value = node.ConfidenceLevel },
                new SqlParameter("@Status", SqlDbType.NVarChar, 20) { Value = node.Status },
                new SqlParameter("@LastUpdated", SqlDbType.DateTime) { Value = node.LastUpdated }
            };

            int result = _database.ExecuteNonQuery(KnowledgeNodeQueries.UpdateNode, parameters);

            // Return true to see if UPDATE was successful
            return result > 0;
        }

        // === DELETE ===
        public bool DeleteNode(int id)
        {
            // Validate Inputs Using Validators 
            FieldValidator.ValidateId(id, "KnowledgeNode ID");


            // DELETE Query + Parameters to delete a KnowledgeNode by ID
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Id", id)
            };

            int result = _database.ExecuteNonQuery(KnowledgeNodeQueries.DeleteNode, parameters);

            // Return true to see if DELETE was successful
            return result > 0;
        }

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */
        private KnowledgeNode ConvertDBRowToClassObj(Dictionary<string, object> rawDBRow)
        {
            return new KnowledgeNode
            {
                Id = Convert.ToInt32(rawDBRow["Id"]),
                Title = rawDBRow["Title"].ToString(),
                DomainId = Convert.ToInt32(rawDBRow["DomainId"]),
                NodeType = rawDBRow["NodeType"].ToString(),
                Description = rawDBRow["Description"].ToString(),
                ConfidenceLevel = Convert.ToInt32(rawDBRow["ConfidenceLevel"]),
                Status = rawDBRow["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                LastUpdated = Convert.ToDateTime(rawDBRow["LastUpdated"])
            };
        }
    }
}
