using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MonBureau.Core.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<int> CountAsync();

        /// <summary>
        /// Gets paged results with optional filtering
        /// PERFORMANCE: Reduces memory usage and query time for large datasets
        /// </summary>
        Task<IEnumerable<T>> GetPagedAsync(
            int skip,
            int take,
            Expression<Func<T, bool>>? filter = null);

        /// <summary>
        /// Gets count of filtered results
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>>? filter = null);
    }
}