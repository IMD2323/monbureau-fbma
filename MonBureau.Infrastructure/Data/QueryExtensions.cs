using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;

namespace MonBureau.Infrastructure.Data
{
    /// <summary>
    /// Type-safe query extensions replace string-based includes
    /// Simpler, safer, and easier to maintain than Repository pattern
    /// </summary>
    public static class QueryExtensions
    {
        /// <summary>
        /// Include related entities for Case
        /// </summary>
        public static IQueryable<Case> WithIncludes(this IQueryable<Case> query)
        {
            return query.Include(c => c.Client);
        }

        /// <summary>
        /// Include related entities for CaseItem
        /// </summary>
        public static IQueryable<CaseItem> WithIncludes(this IQueryable<CaseItem> query)
        {
            return query
                .Include(i => i.Case)
                    .ThenInclude(c => c.Client);
        }

        /// <summary>
        /// Client has no required includes
        /// </summary>
        public static IQueryable<Client> WithIncludes(this IQueryable<Client> query)
        {
            return query;
        }

        /// <summary>
        /// Optional: Include cases count for Client
        /// </summary>
        public static IQueryable<Client> WithCases(this IQueryable<Client> query)
        {
            return query.Include(c => c.Cases);
        }
    }
}