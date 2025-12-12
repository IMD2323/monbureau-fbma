using System.Linq;
using System.Windows;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.UI.ViewModels.Base;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// FIXED: Simplified CasesViewModel
    /// All CRUD logic inherited from CrudViewModelBase
    /// </summary>
    public class CasesViewModel : CrudViewModelBase<Case>
    {
        public CasesViewModel(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        protected override IRepository<Case> GetRepository()
            => _unitOfWork.Cases;

        protected override void ApplyFilter()
        {
            var filtered = FilterByProperties(
                _allItems,
                c => c.Number,
                c => c.Title,
                c => c.Client?.FullName,
                c => c.Description
            ).OrderByDescending(c => c.OpeningDate);

            RefreshItemsCollection(filtered);
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