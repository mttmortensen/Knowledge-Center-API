using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.DataAccess
{
    public static class TagQueries
    {
        public static readonly string InsertTag = @"
            INSERT INTO Tags 
                (Name)
            VALUES 
                (@Name);
        ";

        public static readonly string GetAllTags = @"
            SELECT * FROM Tags;
        ";

        public static readonly string GetTagById = @"
            SELECT * FROM Tags 
            WHERE TagId = @TagId;
        ";

        public static readonly string UpdateTag = @"
            UPDATE Tags 
            SET Name = @Name
            WHERE TagId = @TagId;   
        ";

        public static readonly string DeleteTag = @"
            DELETE FROM Tags 
            WHERE TagId = @TagId;
        ";
    }
}
