using System;
using System.Linq.Expressions;
using System.Windows;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.UI.ViewModels.Base;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.Features.Clients

{
    /// <summary>
    /// FIXED: Database-level filtering instead of in-memory
    /// No more loading all clients into memory
    /// </summary>
    public class ClientsViewModel : CrudViewModelBase<Client>
    {
        public ClientsViewModel(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        protected override IRepository<Client> GetRepository()
            => _unitOfWork.Clients;

        /// <summary>
        /// FIXED: Builds filter expression that executes in DATABASE
        /// Previously filtered 10,000+ clients in memory on every keystroke
        /// Now executes as SQL WHERE clause
        /// </summary>
        protected override Expression<Func<Client, bool>>? BuildFilterExpression(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null; // No filter = all records (paginated)

            var lowerSearch = searchText.ToLowerInvariant();

            // This expression is translated to SQL by Entity Framework
            // Example SQL: WHERE LOWER(FirstName) LIKE '%search%' OR LOWER(LastName) LIKE '%search%'
            return c =>
                (c.FirstName != null && c.FirstName.ToLower().Contains(lowerSearch)) ||
                (c.LastName != null && c.LastName.ToLower().Contains(lowerSearch)) ||
                (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(lowerSearch)) ||
                (c.ContactPhone != null && c.ContactPhone.Contains(searchText)); // Phone exact match
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