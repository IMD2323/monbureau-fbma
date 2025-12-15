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
    /// FIXED: Never loads entire table - always uses pagination
    /// Executes filtering in database (SQL) instead of in-memory
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
            return await _dbSet.FindAsync(id);
        }

        /// <summary>
        /// DEPRECATED - Use GetPagedAsync instead
        /// This method is dangerous for large datasets
        /// </summary>
        [Obsolete("Use GetPagedAsync to avoid loading entire table")]
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            System.Diagnostics.Debug.WriteLine("[Repository] ⚠️ WARNING: GetAllAsync called - use GetPagedAsync instead");

            // FIXED: Still limit to prevent catastrophic memory usage
            var query = ApplyIncludes(_dbSet.AsNoTracking());
            return await query.Take(1000).ToListAsync(); // Safety limit
        }

        /// <summary>
        /// DEPRECATED - Use GetPagedAsync with filter instead
        /// </summary>
        [Obsolete("Use GetPagedAsync with filter parameter")]
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            System.Diagnostics.Debug.WriteLine("[Repository] ⚠️ WARNING: FindAsync called - use GetPagedAsync with filter");

            var query = ApplyIncludes(_dbSet.AsNoTracking());
            return await query.Where(predicate).Take(1000).ToListAsync(); // Safety limit
        }

        /// <summary>
        /// FIXED: Primary method - Always use pagination
        /// Executes filtering in database (SQL Server/SQLite)
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetPagedAsync(
            int skip,
            int take,
            Expression<Func<T, bool>>? filter = null)
        {
            // Validate pagination parameters
            if (skip < 0) skip = 0;
            if (take <= 0 || take > 1000) take = 50; // Max 1000 per page

            IQueryable<T> query = ApplyIncludes(_dbSet.AsNoTracking());

            // Apply filter in database
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // Execute pagination in database
            return await query
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            await _dbSet.AddAsync(entity);
            return entity;
        }

        public virtual Task UpdateAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
                entry.State = EntityState.Modified;
            }

            return Task.CompletedTask;
        }

        public virtual Task DeleteAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }

            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// FIXED: Count all entities efficiently
        /// </summary>
        public virtual async Task<int> CountAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .CountAsync();
        }

        /// <summary>
        /// FIXED: Count with filter - executes in database
        /// </summary>
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? filter)
        {
            if (filter == null)
            {
                return await CountAsync();
            }

            return await _dbSet
                .AsNoTracking()
                .Where(filter)
                .CountAsync();
        }

        /// <summary>
        /// Applies appropriate includes based on entity type
        /// </summary>
        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query)
        {
            var entityType = typeof(T).Name;

            return entityType switch
            {
                "Case" => query
                    .Include("Client")
                    .AsSplitQuery(),

                "CaseItem" => query
                    .Include("Case")
                    .Include("Case.Client")
                    .AsSplitQuery(),

                "Client" => query,

                _ => query
            };
        }
    }
}