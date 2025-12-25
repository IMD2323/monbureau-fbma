using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Interfaces;
using MonBureau.Infrastructure.Data;

namespace MonBureau.Infrastructure.Repositories
{
    /// <summary>
    /// FIXED: Complete rewrite to prevent all tracking conflicts
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            // Load with tracking for updates
            var query = ApplyIncludes(_dbSet);
            var entity = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);

            if (entity != null)
            {
                // Detach to prevent tracking issues
                _context.Entry(entity).State = EntityState.Detached;
            }

            return entity;
        }

        [Obsolete("Use GetPagedAsync to avoid loading entire table")]
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = ApplyIncludes(_dbSet.AsNoTracking());
            return await query.Take(1000).ToListAsync();
        }

        [Obsolete("Use GetPagedAsync with filter parameter")]
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var query = ApplyIncludes(_dbSet.AsNoTracking());
            return await query.Where(predicate).Take(1000).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(
            int skip,
            int take,
            Expression<Func<T, bool>>? filter = null)
        {
            if (skip < 0) skip = 0;
            if (take <= 0 || take > 1000) take = 50;

            IQueryable<T> query = ApplyIncludes(_dbSet.AsNoTracking());

            if (filter != null)
            {
                query = query.Where(filter);
            }

            return await query
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Clear any tracked entities with same ID
            ClearTrackedEntity(entity);

            await _dbSet.AddAsync(entity);
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Clear any tracked entities
            ClearTrackedEntity(entity);

            // Attach and mark as modified
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;

            await Task.CompletedTask;
        }

        public virtual Task DeleteAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Clear any tracked entities
            ClearTrackedEntity(entity);

            // Attach and mark for deletion
            _dbSet.Attach(entity);
            _dbSet.Remove(entity);

            return Task.CompletedTask;
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.AsNoTracking().CountAsync();
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? filter)
        {
            if (filter == null)
            {
                return await CountAsync();
            }

            return await _dbSet.AsNoTracking().Where(filter).CountAsync();
        }

        /// <summary>
        /// Clears any tracked entity with the same ID
        /// </summary>
        private void ClearTrackedEntity(T entity)
        {
            var entityId = GetEntityId(entity);
            if (entityId == 0) return;

            var trackedEntity = _context.ChangeTracker.Entries<T>()
                .FirstOrDefault(e => GetEntityId(e.Entity) == entityId);

            if (trackedEntity != null)
            {
                trackedEntity.State = EntityState.Detached;
            }
        }

        /// <summary>
        /// Gets entity ID using reflection
        /// </summary>
        private int GetEntityId(T entity)
        {
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null)
            {
                return (int)(idProperty.GetValue(entity) ?? 0);
            }
            return 0;
        }

        /// <summary>
        /// Applies appropriate includes based on entity type
        /// </summary>
        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query)
        {
            var entityType = typeof(T).Name;

            return entityType switch
            {
                "Case" => query.Include("Client"),

                "CaseItem" => query
                    .Include("Case")
                    .Include("Case.Client"),

                "Expense" => query
                    .Include("Case")
                    .Include("Case.Client")
                    .Include("AddedByClient"),

                "Appointment" => query
                    .Include("Case")
                    .Include("Case.Client"),

                "Document" => query
                    .Include("Case")
                    .Include("Case.Client")
                    .Include("UploadedByClient"),

                _ => query
            };
        }
    }
}