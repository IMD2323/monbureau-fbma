using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Interfaces;
using MonBureau.Core.Entities;
using MonBureau.Infrastructure.Data;

namespace MonBureau.Infrastructure.Repositories
{
    /// <summary>
    /// FIXED: Strongly-typed includes for proper navigation property loading
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
            var query = ApplyIncludes(_dbSet.AsNoTracking());
            var entity = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
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
            ClearTrackedEntity(entity);
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // CRITICAL FIX: Clear ALL tracked entities of this type
            var trackedEntities = _context.ChangeTracker.Entries<T>()
                .Where(e => e.State != EntityState.Detached)
                .ToList();

            foreach (var tracked in trackedEntities)
            {
                tracked.State = EntityState.Detached;
            }

            // Also clear any related entities that might be tracked
            var allTrackedEntities = _context.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Detached)
                .ToList();

            foreach (var tracked in allTrackedEntities)
            {
                // Don't detach the entity we're about to update
                var trackedId = GetEntityIdGeneric(tracked.Entity);
                var entityId = GetEntityId(entity);

                if (tracked.Entity.GetType() != typeof(T) || trackedId != entityId)
                {
                    tracked.State = EntityState.Detached;
                }
            }

            // Attach and mark as modified
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;

            await Task.CompletedTask;
        }

        private int GetEntityIdGeneric(object entity)
        {
            var idProperty = entity.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                return (int)(idProperty.GetValue(entity) ?? 0);
            }
            return 0;
        }

        public virtual Task DeleteAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            ClearTrackedEntity(entity);
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
        /// FIXED: Strongly-typed includes using lambda expressions
        /// This ensures navigation properties are properly loaded
        /// </summary>
        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query)
        {
            var entityType = typeof(T);

            // Case entity - include Client
            if (entityType == typeof(Case))
            {
                return (IQueryable<T>)((IQueryable<Case>)query)
                    .Include(c => c.Client);
            }

            // CaseItem entity - include Case and Case.Client
            if (entityType == typeof(CaseItem))
            {
                return (IQueryable<T>)((IQueryable<CaseItem>)query)
                    .Include(ci => ci.Case)
                    .ThenInclude(c => c.Client);
            }

            // Expense entity - include Case, Case.Client, and AddedByClient
            if (entityType == typeof(Expense))
            {
                return (IQueryable<T>)((IQueryable<Expense>)query)
                    .Include(e => e.Case)
                        .ThenInclude(c => c.Client)
                    .Include(e => e.AddedByClient);
            }

            // Appointment entity - include Case and Case.Client
            if (entityType == typeof(Appointment))
            {
                return (IQueryable<T>)((IQueryable<Appointment>)query)
                    .Include(a => a.Case)
                        .ThenInclude(c => c.Client);
            }

            // Document entity - include Case, Case.Client, and UploadedByClient
            if (entityType == typeof(Document))
            {
                return (IQueryable<T>)((IQueryable<Document>)query)
                    .Include(d => d.Case)
                        .ThenInclude(c => c.Client)
                    .Include(d => d.UploadedByClient);
            }

            // Default - no includes
            return query;
        }
    }
}