using System.Text.RegularExpressions;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Validation;

namespace MonBureau.Core.Validators
{
    /// <summary>
    /// Unified validator using Fluent API pattern
    /// Replaces ClientValidator, CaseValidator, and inline validation methods
    /// </summary>
    public static class EntityValidator
    {
        #region Common Validators

        public static ValidationResult Required(this ValidationResult result, string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                result.AddError(fieldName, $"{fieldName} est obligatoire");
            return result;
        }

        public static ValidationResult MaxLength(this ValidationResult result, string? value, string fieldName, int max)
        {
            if (!string.IsNullOrEmpty(value) && value.Length > max)
                result.AddError(fieldName, $"{fieldName} ne peut pas dépasser {max} caractères");
            return result;
        }

        public static ValidationResult Email(this ValidationResult result, string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value)) return result;

            try
            {
                var addr = new System.Net.Mail.MailAddress(value);
                if (addr.Address != value)
                    result.AddError(fieldName, "Email invalide");
            }
            catch
            {
                result.AddError(fieldName, "Email invalide");
            }
            return result;
        }

        public static ValidationResult AlgerianPhone(this ValidationResult result, string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value)) return result;

            var cleaned = value.Replace(" ", "").Replace("-", "").Replace(".", "");
            if (!Regex.IsMatch(cleaned, @"^(0[567]\d{8}|(\+213|00213)[567]\d{8})$"))
                result.AddError(fieldName, "Format: 05XX XX XX XX, 06XX XX XX XX, ou 07XX XX XX XX");

            return result;
        }

        public static ValidationResult CaseNumberFormat(this ValidationResult result, string value, string fieldName)
        {
            if (!Regex.IsMatch(value, @"^DOSS-\d{4}-\d{4}$"))
                result.AddError(fieldName, "Format: DOSS-YYYY-NNNN (ex: DOSS-2025-0001)");
            return result;
        }

        public static ValidationResult DateNotFuture(this ValidationResult result, DateTime date, string fieldName)
        {
            if (date > DateTime.Today.AddDays(1))
                result.AddError(fieldName, "Date ne peut pas être dans le futur");
            return result;
        }

        #endregion

        #region Entity Validators

        public static ValidationResult ValidateClient(Client client)
        {
            var result = new ValidationResult();

            if (client == null)
            {
                result.AddGeneralError("Client ne peut pas être null");
                return result;
            }

            return result
                .Required(client.FirstName, nameof(client.FirstName))
                .MaxLength(client.FirstName, nameof(client.FirstName), 100)
                .Required(client.LastName, nameof(client.LastName))
                .MaxLength(client.LastName, nameof(client.LastName), 100)
                .Email(client.ContactEmail, nameof(client.ContactEmail))
                .MaxLength(client.ContactEmail, nameof(client.ContactEmail), 200)
                .AlgerianPhone(client.ContactPhone, nameof(client.ContactPhone))
                .MaxLength(client.Address, nameof(client.Address), 500);
        }

        public static ValidationResult ValidateCase(Case caseEntity)
        {
            var result = new ValidationResult();

            if (caseEntity == null)
            {
                result.AddGeneralError("Dossier ne peut pas être null");
                return result;
            }

            result
                .Required(caseEntity.Number, nameof(caseEntity.Number))
                .MaxLength(caseEntity.Number, nameof(caseEntity.Number), 50)
                .CaseNumberFormat(caseEntity.Number, nameof(caseEntity.Number))
                .Required(caseEntity.Title, nameof(caseEntity.Title))
                .MaxLength(caseEntity.Title, nameof(caseEntity.Title), 200)
                .MaxLength(caseEntity.Description, nameof(caseEntity.Description), 5000)
                .DateNotFuture(caseEntity.OpeningDate, nameof(caseEntity.OpeningDate));

            if (caseEntity.ClientId <= 0)
                result.AddError(nameof(caseEntity.ClientId), "Client valide requis");

            if (caseEntity.ClosingDate.HasValue && caseEntity.ClosingDate < caseEntity.OpeningDate)
                result.AddError(nameof(caseEntity.ClosingDate), "Date de clôture invalide");

            return result;
        }

        public static ValidationResult ValidateCaseItem(CaseItem item)
        {
            var result = new ValidationResult();

            if (item == null)
            {
                result.AddGeneralError("Élément ne peut pas être null");
                return result;
            }

            result
                .Required(item.Name, nameof(item.Name))
                .MaxLength(item.Name, nameof(item.Name), 200)
                .MaxLength(item.Description, nameof(item.Description), 5000)
                .MaxLength(item.FilePath, nameof(item.FilePath), 500);

            if (item.CaseId <= 0)
                result.AddError(nameof(item.CaseId), "Dossier valide requis");

            if (item.Type == ItemType.Expense && (!item.Amount.HasValue || item.Amount < 0))
                result.AddError(nameof(item.Amount), "Montant valide requis pour dépense");

            return result;
        }

        #endregion
    }
}