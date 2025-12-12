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
    /// SIMPLIFIED: Generic repository with optional eager loading configuration
    /// Removes need for specialized CaseRepository and CaseItemRepository
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
            // Use FindAsync for single entity retrieval (tracks by default for updates)
            return await _dbSet.FindAsync(id);
        }

        /// <summary>
        /// OPTIMIZED: Uses AsNoTracking for read-only queries (20-30% faster)
        /// Includes related entities automatically based on entity type
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = ApplyIncludes(_dbSet.AsNoTracking());
            return await query.ToListAsync();
        }

        /// <summary>
        /// OPTIMIZED: Uses AsNoTracking for filtered queries
        /// </summary>
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var query = ApplyIncludes(_dbSet.AsNoTracking());
            return await query.Where(predicate).ToListAsync();
        }

        /// <summary>
        /// OPTIMIZED: Paged queries for large datasets
        /// Performance: Loads only required page instead of entire table
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetPagedAsync(
            int skip,
            int take,
            Expression<Func<T, bool>>? filter = null)
        {
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

            await _dbSet.AddAsync(entity);
            return entity;
        }

        public virtual Task UpdateAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Attach and mark as modified only if not already tracked
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

            // Attach entity if not tracked, then remove
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }

            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// OPTIMIZED: Count all entities
        /// </summary>
        public virtual async Task<int> CountAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .CountAsync();
        }

        /// <summary>
        /// OPTIMIZED: Count with filter
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
        /// NEW: Automatically applies appropriate includes based on entity type
        /// This replaces the need for specialized repositories
        /// </summary>
        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query)
        {
            var entityType = typeof(T).Name;

            // Use AsSplitQuery to prevent cartesian explosion
            return entityType switch
            {
                // Case includes Client
                "Case" => query
                    .Include("Client")
                    .AsSplitQuery(),

                // CaseItem includes Case and nested Client
                "CaseItem" => query
                    .Include("Case")
                    .Include("Case.Client")
                    .AsSplitQuery(),

                // Client has no required includes
                "Client" => query,

                // Default: no includes
                _ => query
            };
        }
    }
}