using System.Threading.Tasks;
using MonBureau.Core.Entities;

namespace MonBureau.Core.Services
{
    /// <summary>
    /// Business rules service for Case operations
    /// Encapsulates complex domain logic
    /// </summary>
    public interface ICaseService
    {
        /// <summary>
        /// Checks if a case can be closed based on business rules
        /// </summary>
        bool CanCloseCase(Case caseEntity);

        /// <summary>
        /// Checks if a case can be reopened
        /// </summary>
        bool CanReopenCase(Case caseEntity);

        /// <summary>
        /// Calculates total expenses for a case
        /// </summary>
        Task<decimal> CalculateTotalExpensesAsync(int caseId);

        /// <summary>
        /// Checks if case has pending documents or tasks
        /// </summary>
        Task<bool> HasPendingItemsAsync(int caseId);

        /// <summary>
        /// Validates case closure and returns validation errors
        /// </summary>
        Task<(bool IsValid, string[] Errors)> ValidateClosureAsync(Case caseEntity);
    }
}