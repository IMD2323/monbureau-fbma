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
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        private IRepository<Client>? _clients;
        private IRepository<Case>? _cases;
        private IRepository<CaseItem>? _caseItems;
        private IRepository<Expense>? _expenses;
        private IRepository<Appointment>? _appointments;
        private IRepository<Document>? _documents;

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IRepository<Client> Clients =>
            _clients ??= new Repository<Client>(_context);

        public IRepository<Case> Cases =>
            _cases ??= new Repository<Case>(_context);

        public IRepository<CaseItem> CaseItems =>
            _caseItems ??= new Repository<CaseItem>(_context);

        public IRepository<Expense> Expenses =>
            _expenses ??= new Repository<Expense>(_context);

        public IRepository<Appointment> Appointments =>
            _appointments ??= new Repository<Appointment>(_context);

        public IRepository<Document> Documents =>
            _documents ??= new Repository<Document>(_context);

        public async Task<int> SaveChangesAsync()
        {
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

        private void UpdateTimestamps()
        {
            var entries = _context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is EntityBase entity)
                {
                    if (entry.State == EntityState.Added)
                        entity.CreatedAt = DateTime.UtcNow;
                    entity.UpdatedAt = DateTime.UtcNow;
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