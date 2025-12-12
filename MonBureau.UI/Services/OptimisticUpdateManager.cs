using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MonBureau.UI.Services
{
    /// <summary>
    /// Manages optimistic UI updates with automatic rollback on failure
    /// </summary>
    public class OptimisticUpdateManager<T> where T : class
    {
        private readonly Stack<UpdateSnapshot<T>> _snapshots = new();

        public class UpdateSnapshot<TEntity>
        {
            public TEntity OriginalEntity { get; set; } = default!;
            public Action RollbackAction { get; set; } = default!;
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Performs an optimistic update with automatic rollback on failure
        /// </summary>
        public async Task<bool> ExecuteWithRollbackAsync(
            T entity,
            Func<Task> updateAction,
            Action optimisticUpdate,
            Action rollbackAction)
        {
            // Take snapshot
            var snapshot = new UpdateSnapshot<T>
            {
                OriginalEntity = entity,
                RollbackAction = rollbackAction,
                Timestamp = DateTime.UtcNow
            };

            _snapshots.Push(snapshot);

            try
            {
                // Apply optimistic update to UI
                optimisticUpdate();

                // Execute actual update
                await updateAction();

                // Success - remove snapshot
                _snapshots.Pop();
                return true;
            }
            catch (Exception ex)
            {
                // Rollback optimistic changes
                System.Diagnostics.Debug.WriteLine($"[OptimisticUpdate] Rolling back: {ex.Message}");
                rollbackAction();
                _snapshots.Pop();
                return false;
            }
        }

        /// <summary>
        /// Rollback all pending snapshots
        /// </summary>
        public void RollbackAll()
        {
            while (_snapshots.Count > 0)
            {
                var snapshot = _snapshots.Pop();
                snapshot.RollbackAction();
            }
        }

        /// <summary>
        /// Gets the number of pending snapshots
        /// </summary>
        public int PendingSnapshotCount => _snapshots.Count;
    }
}
