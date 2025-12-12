using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonBureau.Core.Interfaces;

namespace MonBureau.Infrastructure.Services
{
    /// <summary>
    /// Service for bulk operations with progress tracking and error recovery
    /// </summary>
    public class BulkOperationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BulkOperationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public class BulkOperationResult
        {
            public int TotalItems { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public List<string> Errors { get; set; } = new();
            public bool IsSuccess => FailureCount == 0;
            public double SuccessRate => TotalItems > 0 ? (double)SuccessCount / TotalItems * 100 : 0;
        }

        /// <summary>
        /// Performs bulk delete operation with progress tracking
        /// </summary>
        public async Task<BulkOperationResult> BulkDeleteAsync<T>(
            IEnumerable<T> items,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var result = new BulkOperationResult();
            var itemsList = items.ToList();
            result.TotalItems = itemsList.Count;

            var repository = GetRepository<T>();
            var processedCount = 0;

            foreach (var item in itemsList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await repository.DeleteAsync(item);
                    await _unitOfWork.SaveChangesAsync();
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Errors.Add($"Erreur de suppression: {ex.Message}");
                }

                processedCount++;
                progress?.Report((int)((double)processedCount / result.TotalItems * 100));
            }

            return result;
        }

        /// <summary>
        /// Performs bulk update operation
        /// </summary>
        public async Task<BulkOperationResult> BulkUpdateAsync<T>(
            IEnumerable<T> items,
            Action<T> updateAction,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var result = new BulkOperationResult();
            var itemsList = items.ToList();
            result.TotalItems = itemsList.Count;

            var repository = GetRepository<T>();
            var processedCount = 0;

            foreach (var item in itemsList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    updateAction(item);
                    await repository.UpdateAsync(item);
                    await _unitOfWork.SaveChangesAsync();
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Errors.Add($"Erreur de mise à jour: {ex.Message}");
                }

                processedCount++;
                progress?.Report((int)((double)processedCount / result.TotalItems * 100));
            }

            return result;
        }

        private IRepository<T> GetRepository<T>() where T : class
        {
            var type = typeof(T);

            if (type == typeof(MonBureau.Core.Entities.Client))
                return (IRepository<T>)_unitOfWork.Clients;
            if (type == typeof(MonBureau.Core.Entities.Case))
                return (IRepository<T>)_unitOfWork.Cases;
            if (type == typeof(MonBureau.Core.Entities.CaseItem))
                return (IRepository<T>)_unitOfWork.CaseItems;

            throw new NotSupportedException($"Repository for type {type.Name} is not supported");
        }
    }
}