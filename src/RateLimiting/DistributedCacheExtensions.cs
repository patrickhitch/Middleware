using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Hellang.Middleware.RateLimiting
{
    public static class DistributedCacheExtensions
    {
        public static async Task<int> CountAsync(this IDistributedCache cache, string key, TimeSpan period, CancellationToken cancellationToken = default)
        {
            var periodSeconds = (int)period.TotalSeconds;
            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiresIn = TimeSpan.FromSeconds(periodSeconds - (nowSeconds % periodSeconds));

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiresIn
            };

            var prefixedKey = $"rate-limit:{nowSeconds / periodSeconds}:{key}";

            var countBytes = await cache.GetAsync(prefixedKey, cancellationToken);

            var count = BitConverter.ToInt32(countBytes, startIndex: 0);

            var newCountBytes = BitConverter.GetBytes(++count);

            await cache.SetAsync(key, newCountBytes, options, cancellationToken);

            return count;
        }
    }
}