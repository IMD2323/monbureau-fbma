using System.Linq;
using System.Windows;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.UI.ViewModels.Base;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// SIMPLIFIED: Only 40 lines! (was 200+ lines)
    /// All CRUD logic inherited from CrudViewModelBase
    /// </summary>
    public class ClientsViewModel : CrudViewModelBase<Client>
    {
        public ClientsViewModel(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        protected override IRepository<Client> GetRepository()
            => _unitOfWork.Clients;

        protected override void ApplyFilter()
        {
            var filtered = FilterByProperties(
                _allItems,
                c => c.FirstName,
                c => c.LastName,
                c => c.ContactEmail,
                c => c.ContactPhone
            ).OrderByDescending(c => c.CreatedAt);

            RefreshItemsCollection(filtered);
        }

        protected override Window CreateAddDialog()
            => new ClientDialog();

        protected override Window CreateEditDialog(Client entity)
            => new ClientDialog(entity);

        protected override string GetEntityName()
            => "Client";

        protected override string GetEntityPluralName()
            => "Clients";

        protected override string GetEntityDisplayName(Client entity)
            => entity.FullName;
    }
}