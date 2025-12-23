using System;
using System.Linq.Expressions;
using System.Windows;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.UI.ViewModels.Base;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// FIXED: Database-level filtering for Cases
    /// </summary>
    public class CasesViewModel : CrudViewModelBase<Case>
    {
        public CasesViewModel(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        protected override IRepository<Case> GetRepository()
            => _unitOfWork.Cases;

        /// <summary>
        /// FIXED: Executes in database as SQL WHERE clause
        /// Includes related Client data in filter
        /// </summary>
        protected override Expression<Func<Case, bool>>? BuildFilterExpression(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            var lowerSearch = searchText.ToLowerInvariant();

            // Entity Framework translates this to SQL JOIN + WHERE
            return c =>
                (c.Number != null && c.Number.ToLower().Contains(lowerSearch)) ||
                (c.Title != null && c.Title.ToLower().Contains(lowerSearch)) ||
                (c.Description != null && c.Description.ToLower().Contains(lowerSearch)) ||
                (c.Client != null &&
                    ((c.Client.FirstName != null && c.Client.FirstName.ToLower().Contains(lowerSearch)) ||
                     (c.Client.LastName != null && c.Client.LastName.ToLower().Contains(lowerSearch))));
        }

        protected override Window CreateAddDialog()
            => new CaseDialog();

        protected override Window CreateEditDialog(Case entity)
            => new CaseDialog(entity);

        protected override string GetEntityName()
            => "Dossier";

        protected override string GetEntityPluralName()
            => "Dossiers";

        protected override string GetEntityDisplayName(Case entity)
            => $"{entity.Number} - {entity.Title}";
    }
}