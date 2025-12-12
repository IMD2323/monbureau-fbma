using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.Infrastructure.Data;

namespace MonBureau.Infrastructure.Repositories
{
    /// <summary>
    /// SIMPLIFIED: Uses generic Repository<T> for all entities
    /// Removed specialized CaseRepository and CaseItemRepository
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        private IRepository<Client>? _clients;
        private IRepository<Case>? _cases;
        private IRepository<CaseItem>? _caseItems;

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // SIMPLIFIED: All repositories use generic Repository<T>
        // Eager loading is handled automatically in Repository.ApplyIncludes()
        public IRepository<Client> Clients =>
            _clients ??= new Repository<Client>(_context);

        public IRepository<Case> Cases =>
            _cases ??= new Repository<Case>(_context);

        public IRepository<CaseItem> CaseItems =>
            _caseItems ??= new Repository<CaseItem>(_context);

        public async Task<int> SaveChangesAsync()
        {
            // OPTIMIZED: Update timestamps automatically
            UpdateTimestamps();

            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                await SaveChangesAsync();

                if (_transaction != null)
                {
                    await _transaction.CommitAsync();
                }
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// OPTIMIZED: Automatically update UpdatedAt timestamps
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = _context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is Client client)
                {
                    if (entry.State == EntityState.Added)
                        client.CreatedAt = DateTime.UtcNow;
                    client.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Case caseEntity)
                {
                    if (entry.State == EntityState.Added)
                        caseEntity.CreatedAt = DateTime.UtcNow;
                    caseEntity.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is CaseItem item)
                {
                    if (entry.State == EntityState.Added)
                        item.CreatedAt = DateTime.UtcNow;
                    item.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context?.Dispose();
        }
    }
}