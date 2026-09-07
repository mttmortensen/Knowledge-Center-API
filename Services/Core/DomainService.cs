using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Domains;
using Knowledge_Center_API.Models.KnowledgeNodes;
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
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@DomainName", NpgsqlDbType.Varchar, 100) { Value = domain.DomainName },
                new NpgsqlParameter("@DomainDescription", NpgsqlDbType.Varchar, 300) { Value = domain.DomainDescription ?? string.Empty },
                new NpgsqlParameter("@DomainStatus", NpgsqlDbType.Varchar, 20) { Value = domain.DomainStatus },
                new NpgsqlParameter("@CreatedAt", NpgsqlDbType.Timestamp) { Value = domain.CreatedAt },
                new NpgsqlParameter("@LastUsed", NpgsqlDbType.Timestamp) { Value = domain.LastUsed }
            };

            // Run the INSERT query and capture the DB-generated id
            int newDomainId = _database.ExecuteScalar<int>(DomainQueries.InsertDomain, parameters);
            if (newDomainId <= 0)
                return false;

            domain.DomainId = newDomainId;
            return true;
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

            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@DomainId", id)
            };

            var rawDBResults = _database.ExecuteQuery(DomainQueries.GetDomainById, parameters);


            if (rawDBResults.Count == 0)
            {
                return null; // No domain found with the given ID
            }

            DomainWithKNsDto domainDto = ConvertDBRowToDomainWithKNsObj(rawDBResults[0]);

            List<KnowledgeNode> nodes = GetKnowledgeNodesForDomain(id);

            domainDto.KnowledgeNodes = nodes.Select(node => new KnowledgeNodeInlineDto
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
            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@DomainId", domainId)
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
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@DomainId", NpgsqlDbType.Integer) { Value = domainId }
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
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@DomainId", NpgsqlDbType.Integer) { Value = domain.DomainId },
                new NpgsqlParameter("@DomainName", NpgsqlDbType.Varchar, 100) { Value = domain.DomainName },
                new NpgsqlParameter("@DomainDescription", NpgsqlDbType.Varchar, 300) { Value = domain.DomainDescription ?? string.Empty },
                new NpgsqlParameter("@DomainStatus", NpgsqlDbType.Varchar, 20) { Value = domain.DomainStatus },
                new NpgsqlParameter("@LastUsed", NpgsqlDbType.Timestamp) { Value = domain.LastUsed }
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
            var parameters = new List<NpgsqlParameter>
            {
               new NpgsqlParameter("@DomainId", NpgsqlDbType.Integer) { Value = domainId }
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
                KnowledgeNodes = new List<KnowledgeNodeInlineDto>()
            };
        }
    }
}
