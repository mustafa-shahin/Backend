using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching
{
    /// <summary>
    /// Cache statistics for monitoring
    /// </summary>
    public class CacheStatistics
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long ErrorCount { get; set; }
        public double HitRatio => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public long TotalOperations => HitCount + MissCount + ErrorCount;
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;
        public Dictionary<string, long> OperationCounts { get; set; } = new();
    }
}
