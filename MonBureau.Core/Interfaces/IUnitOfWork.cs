using System;
using System.Threading.Tasks;
using MonBureau.Core.Entities;

namespace MonBureau.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        // Existing repositories
        IRepository<Client> Clients { get; }
        IRepository<Case> Cases { get; }
        IRepository<CaseItem> CaseItems { get; }

        // NEW: Add repositories for new features
        IRepository<Expense> Expenses { get; }
        IRepository<Appointment> Appointments { get; }
        IRepository<Document> Documents { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}