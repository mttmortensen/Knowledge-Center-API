using Knowledge_Center_API.Models;
using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Services.Validation;
using Microsoft.Data.SqlClient;
using System.Data;


namespace Knowledge_Center_API.Services.Core
{
    public class TagService
    {
        private readonly Database _db;
        public TagService(Database db)
        {
            _db = db;
        }

        /* ===================== CRUD ===================== */

        // Create
        public bool CreateTag(Tags tag)
        {
            // Validate Input Fields
            FieldValidator.ValidateRequiredString(tag.Name, "Tag Name", 100);

            //Build SQL Parameters
            List<SqlParameter> tagParameters = new List<SqlParameter>
            {
                new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = tag.Name }
            };

            // Run the INSERT Query
            int result = _db.ExecuteNonQuery(TagQueries.InsertTag, tagParameters);

            // Return true to see if INSERT was successful
            return result > 0;
        }

        // Read
        public List<Tags> GetAllTags()
        {
            List<Tags> tags = new List<Tags>();

            // Execute the SELECT query to get all tags
            var rawDBResults = _db.ExecuteQuery(TagQueries.GetAllTags, null);

            // Map each row to a Tags object
            foreach (var rawDBRow in rawDBResults)
            {
                tags.Add(ConvertDBRowToClassObj(rawDBRow));
            }

            return tags;
        }

        public Tags GetTagById(int tagId)
        {
            // Validate Input Fields
            FieldValidator.ValidateId(tagId, "Tag ID");

            // Build SQL Parameters
            List<SqlParameter> tagParameters = new List<SqlParameter>
            {
                new SqlParameter("@TagId", SqlDbType.Int) { Value = tagId }
            };

            // Execute the SELECT query to get a specific tag by ID
            var rawDBResults = _db.ExecuteQuery(TagQueries.GetTagById, tagParameters);

            // If no results, return null
            if (rawDBResults.Count == 0) return null;

            // Map the first row to a Tags object
            return ConvertDBRowToClassObj(rawDBResults[0]);
        }

        // Update
        public bool UpdateTag(Tags tag)
        {
            // Validate Input Fields
            FieldValidator.ValidateId(tag.TagId, "Tag ID");
            FieldValidator.ValidateRequiredString(tag.Name, "Tag Name", 100);

            // Build SQL Parameters
            List<SqlParameter> tagParameters = new List<SqlParameter>
            {
                new SqlParameter("@TagId", SqlDbType.Int) { Value = tag.TagId },
                new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = tag.Name }
            };

            // Run the UPDATE Query
            int result = _db.ExecuteNonQuery(TagQueries.UpdateTag, tagParameters);

            // Return true to see if UPDATE was successful
            return result > 0;
        }

        // Delete
        public bool DeleteTag(int tagId)
        {
            // Validate Input Fields
            FieldValidator.ValidateId(tagId, "Tag ID");

            // Build SQL Parameters
            List<SqlParameter> tagParameters = new List<SqlParameter>
            {
                new SqlParameter("@TagId", SqlDbType.Int) { Value = tagId }
            };

            // Run the DELETE Query
            int result = _db.ExecuteNonQuery(TagQueries.DeleteTag, tagParameters);

            // Return true to see if DELETE was successful
            return result > 0;
        }

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */

        private Tags ConvertDBRowToClassObj(Dictionary<string, object> rawDBRow)
        {
            return new Tags
            {
                TagId = Convert.ToInt32(rawDBRow["TagId"]),
                Name = rawDBRow["Name"].ToString()
            };
        }

    }
}
