using System;
using System.Threading.Tasks;
using MonBureau.Core.Entities;

namespace MonBureau.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Client> Clients { get; }
        IRepository<Case> Cases { get; }
        IRepository<CaseItem> CaseItems { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
