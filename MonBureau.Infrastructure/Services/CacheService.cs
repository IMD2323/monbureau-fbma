using System;
using System.Collections.Generic;

namespace MonBureau.Infrastructure.Services
{
    /// <summary>
    /// In-memory cache service for query results
    /// Improves performance by reducing database queries
    /// </summary>
    public class CacheService
    {
        private readonly Dictionary<string, CacheEntry> _cache = new();
        private readonly object _lock = new();

        /// <summary>
        /// Gets cached value by key
        /// </summary>
        public T? Get<T>(string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (entry.ExpiresAt > DateTime.UtcNow)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CacheService] Cache HIT: {key}");
                        return (T?)entry.Value;
                    }

                    // Expired - remove it
                    _cache.Remove(key);
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Cache EXPIRED: {key}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CacheService] Cache MISS: {key}");
            return default;
        }

        /// <summary>
        /// Sets cached value with expiration
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan duration)
        {
            lock (_lock)
            {
                _cache[key] = new CacheEntry
                {
                    Value = value,
                    ExpiresAt = DateTime.UtcNow + duration
                };

                System.Diagnostics.Debug.WriteLine($"[CacheService] Cache SET: {key} (expires in {duration.TotalMinutes:F1} min)");
            }
        }

        /// <summary>
        /// Invalidates specific cache key
        /// </summary>
        public void Invalidate(string key)
        {
            lock (_lock)
            {
                if (_cache.Remove(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Cache INVALIDATED: {key}");
                }
            }
        }

        /// <summary>
        /// Invalidates all cache keys matching pattern
        /// </summary>
        public void InvalidatePattern(string pattern)
        {
            lock (_lock)
            {
                var keysToRemove = new List<string>();

                foreach (var key in _cache.Keys)
                {
                    if (key.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Cache INVALIDATED (pattern): {key}");
                }
            }
        }

        /// <summary>
        /// Clears all cached data
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var count = _cache.Count;
                _cache.Clear();
                System.Diagnostics.Debug.WriteLine($"[CacheService] Cache CLEARED: {count} entries");
            }
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStats GetStats()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var expired = 0;

                foreach (var entry in _cache.Values)
                {
                    if (entry.ExpiresAt <= now)
                    {
                        expired++;
                    }
                }

                return new CacheStats
                {
                    TotalEntries = _cache.Count,
                    ActiveEntries = _cache.Count - expired,
                    ExpiredEntries = expired
                };
            }
        }

        /// <summary>
        /// Removes expired entries (garbage collection)
        /// </summary>
        public void Cleanup()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var keysToRemove = new List<string>();

                foreach (var kvp in _cache)
                {
                    if (kvp.Value.ExpiresAt <= now)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }

                if (keysToRemove.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Cleaned up {keysToRemove.Count} expired entries");
                }
            }
        }

        private class CacheEntry
        {
            public object? Value { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }

    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public int ActiveEntries { get; set; }
        public int ExpiredEntries { get; set; }
    }
}