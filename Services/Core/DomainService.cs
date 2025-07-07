using Knowledge_Center.Models;
using Knowledge_Center.Services.Validation;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center.Services.Core
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
                domains.Add(ConvertDBRowToClassObj(rawDBRow));
            }

            return domains;
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
            Domain domain = ConvertDBRowToClassObj(rawDBRow);
            return domain;
        }

        // === UPDATE ===
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

        private Domain ConvertDBRowToClassObj(Dictionary<string, object> rawDBRow)
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
    }
}
