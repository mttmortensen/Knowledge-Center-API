using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center.Services.Validation
{
    public class FieldValidator
    {
        public static void ValidateRequiredString(string value, string fieldName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{fieldName} is required.");

            if (value.Length > maxLength)
                throw new ArgumentException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        public static void ValidateOptionalString(string value, string fieldName, int maxLength)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
                throw new ArgumentException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        public static void ValidateEnumValue(string value, string fieldName, HashSet<string> validValues)
        {
            if (!validValues.Contains(value))
                throw new ArgumentException($"Invalid {fieldName}: {value}");
        }

        public static void ValidateId(int id, string fieldName = "ID")
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(fieldName, $"Invalid {fieldName}.");
        }

    }
}
