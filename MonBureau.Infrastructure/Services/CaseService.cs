using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Services;
using MonBureau.Infrastructure.Data;

namespace MonBureau.Infrastructure.Services
{
    public class CaseService : ICaseService
    {
        private readonly AppDbContext _context;

        public CaseService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool CanCloseCase(Case caseEntity)
        {
            if (caseEntity == null)
                return false;

            // Cannot close an already closed or archived case
            if (caseEntity.Status == CaseStatus.Closed || caseEntity.Status == CaseStatus.Archived)
                return false;

            return true;
        }

        public bool CanReopenCase(Case caseEntity)
        {
            if (caseEntity == null)
                return false;

            // Can only reopen closed cases
            return caseEntity.Status == CaseStatus.Closed;
        }

        public async Task<decimal> CalculateTotalExpensesAsync(int caseId)
        {
            var total = await _context.CaseItems
                .AsNoTracking()
                .Where(i => i.CaseId == caseId && i.Type == ItemType.Expense && i.Amount.HasValue)
                .SumAsync(i => i.Amount ?? 0);

            return total;
        }

        public async Task<bool> HasPendingItemsAsync(int caseId)
        {
            // Check for items with future dates (pending tasks/hearings)
            var hasPending = await _context.CaseItems
                .AsNoTracking()
                .AnyAsync(i => i.CaseId == caseId &&
                              (i.Type == ItemType.Task || i.Type == ItemType.Hearing) &&
                              i.Date > DateTime.Today);

            return hasPending;
        }

        public async Task<(bool IsValid, string[] Errors)> ValidateClosureAsync(Case caseEntity)
        {
            var errors = new List<string>();

            if (caseEntity == null)
            {
                errors.Add("Le dossier n'existe pas");
                return (false, errors.ToArray());
            }

            // Check if already closed
            if (!CanCloseCase(caseEntity))
            {
                errors.Add("Ce dossier ne peut pas être fermé dans son état actuel");
            }

            // Check for pending items
            if (await HasPendingItemsAsync(caseEntity.Id))
            {
                errors.Add("Le dossier contient des tâches ou audiences en attente");
            }

            // Validate minimum case duration (optional business rule)
            var daysSinceOpening = (DateTime.Today - caseEntity.OpeningDate).Days;
            if (daysSinceOpening < 1)
            {
                errors.Add("Le dossier ne peut être fermé le jour de son ouverture");
            }

            return (errors.Count == 0, errors.ToArray());
        }
    }
}