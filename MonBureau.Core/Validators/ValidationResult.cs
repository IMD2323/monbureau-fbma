using System.Collections.Generic;
using System.Linq;

namespace MonBureau.Core.Validation
{
    /// <summary>
    /// Structured validation result with field-level errors
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public Dictionary<string, List<string>> Errors { get; } = new();
        public List<string> GeneralErrors { get; } = new();

        public void AddError(string field, string message)
        {
            if (!Errors.ContainsKey(field))
            {
                Errors[field] = new List<string>();
            }
            Errors[field].Add(message);
        }

        public void AddGeneralError(string message)
        {
            GeneralErrors.Add(message);
        }

        public List<string> GetErrors(string field)
        {
            return Errors.ContainsKey(field) ? Errors[field] : new List<string>();
        }

        public string GetFirstError(string field)
        {
            var errors = GetErrors(field);
            return errors.Any() ? errors.First() : string.Empty;
        }

        public List<string> GetAllErrors()
        {
            var allErrors = new List<string>();
            allErrors.AddRange(GeneralErrors);
            foreach (var fieldErrors in Errors.Values)
            {
                allErrors.AddRange(fieldErrors);
            }
            return allErrors;
        }

        public string GetFormattedErrors()
        {
            return string.Join("\n", GetAllErrors());
        }
    }
}
