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
    public class DomainService
    {
        private readonly Database _database;
        public DomainService(Database database)
        {
            _database = database;
        }

        /* ===================== CRUD ===================== */

        // === CREATE ===
        public bool CreateDomain(Domain domain)
        {
            // Validating Field Inputs 
            FieldValidator.ValidateRequiredString(domain.DomainName, "Domain Name", 100);
            FieldValidator.ValidateOptionalString(domain.DomainDescription, "Domain Description", 300);
            FieldValidator.ValidateEnumValue(domain.DomainStatus, "Domain Status", new() { "Active", "Inactive" });

            // Set timestamps first
            DateTime now = DateTime.Now;
            domain.CreatedAt = now;
            domain.LastUsed = now;

            // Build SQL Parameters
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@DomainName", SqlDbType.NVarChar, 100) { Value = domain.DomainName },
                new SqlParameter("@DomainDescription", SqlDbType.NVarChar, 300) { Value = domain.DomainDescription ?? string.Empty },
                new SqlParameter("@DomainStatus", SqlDbType.NVarChar, 20) { Value = domain.DomainStatus },
                new SqlParameter("@CreatedAt", SqlDbType.DateTime) { Value = domain.CreatedAt },
                new SqlParameter("@LastUsed", SqlDbType.DateTime) { Value = domain.LastUsed }
            };

            // Run the INSERT query
            int result = _database.ExecuteNonQuery(DomainQueries.InsertDomain, parameters);

            // Return true to see if INSERT was successful
            return result > 0;
        }

        // === READ ===
        public List<Domain> GetAllDomains()
        {
            List<Domain> domains = new List<Domain>();

            var rawDBResults = _database.ExecuteQuery(DomainQueries.GetAllDomains, null);

            foreach (var rawDBRow in rawDBResults)
            {
                domains.Add(ConvertDBRowToDomainBaseObj(rawDBRow));
            }

            return domains;
        }

        public DomainWithKNsDto GetDomainByIdWithKNs(int id) 
        {
            FieldValidator.ValidateId(id, "Domain ID");

            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@DomainId", id)
            };

            var rawDBResults = _database.ExecuteQuery(DomainQueries.GetDomainById, parameters);


            if (rawDBResults.Count == 0)
            {
                return null; // No domain found with the given ID
            }

            DomainWithKNsDto domainDto = ConvertDBRowToDomainWithKNsObj(rawDBResults[0]);

            List<KnowledgeNode> nodes = GetKnowledgeNodesForDomain(id);

            domainDto.KnowledgNodes = nodes.Select(node => new KnowledgeNodeInlineDto
            {
                Id = node.Id,
                Title = node.Title,
                NodeType = node.NodeType,
                ConfidenceLevel = node.ConfidenceLevel,
                Status = node.Status,
                CreatedAt = node.CreatedAt,
                LastUpdated = node.LastUpdated
            })
            .ToList();

            return domainDto;
        }

        private List<KnowledgeNode> GetKnowledgeNodesForDomain(int domainId)
        {
            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@DomainId", domainId)
            };

            var rawDBResults = _database.ExecuteQuery(KnowledgeNodeQueries.GetKnowledgeNodesByDomainId, parameters);

            return rawDBResults.Select(row => new KnowledgeNode
            {
                Id = Convert.ToInt32(row["Id"]),
                Title = row["Title"].ToString(),
                DomainId = Convert.ToInt32(row["DomainId"]),
                NodeType = row["NodeType"].ToString(),
                Description = row["Description"].ToString(),
                ConfidenceLevel = Convert.ToInt32(row["ConfidenceLevel"]),
                Status = row["Status"].ToString(),
                CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
                LastUpdated = Convert.ToDateTime(row["LastUpdated"])

            })
            .ToList();
        }

        public Domain GetDomainById(int domainId)
        {
            // Validating Field Input 
            FieldValidator.ValidateId(domainId, "Domain ID");

            // Build SQL Parameters
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@DomainId", SqlDbType.Int) { Value = domainId }
            };

            // SELECT Query + Parameters to retrieve a single Domain and map results into Domain object
            var rawDBResults = _database.ExecuteQuery(DomainQueries.GetDomainById, parameters);

            if (rawDBResults.Count == 0)
            {
                return null; // No domain found with the given ID
            }

            // Map the first result to a Domain object
            var rawDBRow = rawDBResults.First();
            Domain domain = ConvertDBRowToDomainBaseObj(rawDBRow);
            return domain;
        }

        // === UPDATE ===

        // This update method is what can allow to not have every field 
        // updated. This will return the Dto for Domain which allows for the
        // '?' fields
        public bool UpdateDomainFromDto(int domainId, DomainUpdateDto dto)
        {
            var existing = GetDomainById(domainId);
            if (existing == null)
                return false;

            // Only update fields that were sent
            if (!string.IsNullOrWhiteSpace(dto.DomainName)) existing.DomainName = dto.DomainName;
            if (!string.IsNullOrWhiteSpace(dto.DomainDescription)) existing.DomainDescription = dto.DomainDescription;
            if (!string.IsNullOrWhiteSpace(dto.DomainStatus)) existing.DomainStatus = dto.DomainStatus;

            existing.LastUpdated = DateTime.Now;

            return UpdateDomain(existing);
        }
        public bool UpdateDomain(Domain domain)
        {
            // Validating Field Inputs 
            FieldValidator.ValidateId(domain.DomainId, "Domain ID");
            FieldValidator.ValidateRequiredString(domain.DomainName, "Domain Name", 100);
            FieldValidator.ValidateOptionalString(domain.DomainDescription, "Domain Description", 300);
            FieldValidator.ValidateEnumValue(domain.DomainStatus, "Domain Status", new() { "Active", "Inactive" });

            // Build SQL Parameters
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@DomainId", SqlDbType.Int) { Value = domain.DomainId },
                new SqlParameter("@DomainName", SqlDbType.NVarChar, 100) { Value = domain.DomainName },
                new SqlParameter("@DomainDescription", SqlDbType.NVarChar, 300) { Value = domain.DomainDescription ?? string.Empty },
                new SqlParameter("@DomainStatus", SqlDbType.NVarChar, 20) { Value = domain.DomainStatus },
                new SqlParameter("@LastUsed", SqlDbType.DateTime) { Value = domain.LastUsed }
            };

            // Run the UPDATE query
            int result = _database.ExecuteNonQuery(DomainQueries.UpdateDomain, parameters);

            // Return true to see if UPDATE was successful
            return result > 0;
        }

        // === DELETE ===
        public bool DeleteDomain(int domainId)
        {
            // Validating Field Input 
            FieldValidator.ValidateId(domainId, "Domain ID");

            // Build SQL Parameters
            var parameters = new List<SqlParameter>
            {
               new SqlParameter("@DomainId", SqlDbType.Int) { Value = domainId }
            };

            // Run the DELETE query
            int result = _database.ExecuteNonQuery(DomainQueries.DeleteDomain, parameters);

            // Return true to see if DELETE was successful
            return result > 0;
        }

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */

        private Domain ConvertDBRowToDomainBaseObj(Dictionary<string, object> rawDBRow)
        {
            return new Domain
            {
                DomainId = Convert.ToInt32(rawDBRow["DomainId"]),
                DomainName = rawDBRow["DomainName"].ToString(),
                DomainDescription = rawDBRow["DomainDescription"].ToString(),
                DomainStatus = rawDBRow["DomainStatus"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                LastUsed = Convert.ToDateTime(rawDBRow["LastUsed"])
            };
        }

        private DomainWithKNsDto ConvertDBRowToDomainWithKNsObj(Dictionary<string, object> rawDBRow)
        {
            return new DomainWithKNsDto
            {
                DomainId = Convert.ToInt32(rawDBRow["DomainId"]),
                DomainName = rawDBRow["DomainName"].ToString(),
                DomainDescription = rawDBRow["DomainDescription"].ToString(),
                DomainStatus = rawDBRow["DomainStatus"].ToString(),
                CreatedAt = Convert.ToDateTime(rawDBRow["CreatedAt"]),
                LastUsed = Convert.ToDateTime(rawDBRow["LastUsed"]),
                KnowledgNodes = new List<KnowledgeNodeInlineDto>()
            };
        }
    }
}
