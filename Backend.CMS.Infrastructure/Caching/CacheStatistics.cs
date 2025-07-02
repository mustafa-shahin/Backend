using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Caching
{
    /// <summary>
    /// Cache statistics for monitoring
    /// </summary>
    public class CacheStatistics
    {
        private long _hitCount;
        private long _missCount;
        private long _errorCount;

        public long HitCount
        {
            get => _hitCount;
            set => _hitCount = value;
        }

        public long MissCount
        {
            get => _missCount;
            set => _missCount = value;
        }

        public long ErrorCount
        {
            get => _errorCount;
            set => _errorCount = value;
        }

        public double HitRatio => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public long TotalOperations => HitCount + MissCount + ErrorCount;
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;
        public ConcurrentDictionary<string, long> OperationCounts { get; set; } = new();

        /// <summary>
        /// Thread-safe increment for hit count
        /// </summary>
        public void IncrementHits() => Interlocked.Increment(ref _hitCount);

        /// <summary>
        /// Thread-safe increment for miss count
        /// </summary>
        public void IncrementMisses() => Interlocked.Increment(ref _missCount);

        /// <summary>
        /// Thread-safe increment for error count
        /// </summary>
        public void IncrementErrors() => Interlocked.Increment(ref _errorCount);

        /// <summary>
        /// Reset all counters
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
            Interlocked.Exchange(ref _errorCount, 0);
            LastResetTime = DateTime.UtcNow;
            OperationCounts.Clear();
        }

        /// <summary>
        /// Thread-safe increment for operation count
        /// </summary>
        public void IncrementOperation(string operation)
        {
            OperationCounts.AddOrUpdate(operation, 1, (key, count) => count + 1);
        }
    }
}