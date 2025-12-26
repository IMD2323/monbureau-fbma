using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MonBureau.Infrastructure.Services
{
    /// <summary>
    /// FIXED: Using ConcurrentDictionary for thread-safe operations without locks
    /// Improves performance and prevents UI freezes
    /// </summary>
    public class CacheService
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        /// <summary>
        /// Gets cached value by key
        /// </summary>
        public T? Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Cache HIT: {key}");
                    return (T?)entry.Value;
                }

                // Expired - try to remove it
                _cache.TryRemove(key, out _);
                System.Diagnostics.Debug.WriteLine($"[CacheService] Cache EXPIRED: {key}");
            }

            System.Diagnostics.Debug.WriteLine($"[CacheService] Cache MISS: {key}");
            return default;
        }

        /// <summary>
        /// Sets cached value with expiration
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan duration)
        {
            _cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow + duration
            };

            System.Diagnostics.Debug.WriteLine($"[CacheService] Cache SET: {key} (expires in {duration.TotalMinutes:F1} min)");
        }

        /// <summary>
        /// Invalidates specific cache key
        /// </summary>
        public void Invalidate(string key)
        {
            if (_cache.TryRemove(key, out _))
            {
                System.Diagnostics.Debug.WriteLine($"[CacheService] Cache INVALIDATED: {key}");
            }
        }

        /// <summary>
        /// Invalidates all cache keys matching pattern
        /// </summary>
        public void InvalidatePattern(string pattern)
        {
            var keysToRemove = _cache.Keys
                .Where(key => key.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
                System.Diagnostics.Debug.WriteLine($"[CacheService] Cache INVALIDATED (pattern): {key}");
            }
        }

        /// <summary>
        /// Clears all cached data
        /// </summary>
        public void Clear()
        {
            var count = _cache.Count;
            _cache.Clear();
            System.Diagnostics.Debug.WriteLine($"[CacheService] Cache CLEARED: {count} entries");
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStats GetStats()
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

        /// <summary>
        /// Removes expired entries (garbage collection)
        /// </summary>
        public void Cleanup()
        {
            var now = DateTime.UtcNow;
            var keysToRemove = _cache
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[CacheService] Cleaned up {keysToRemove.Count} expired entries");
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