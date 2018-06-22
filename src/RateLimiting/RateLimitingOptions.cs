using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;

namespace Hellang.Middleware.RateLimiting
{
    public class RateLimitingOptions
    {
        private List<Check<bool>> Safelists { get; } = new List<Check<bool>>();

        private List<Check<bool>> Blocklists { get; } = new List<Check<bool>>();

        private List<ThrottleCheck> Throttles { get; } = new List<ThrottleCheck>();

        public void Safelist(string name, Func<HttpContext, bool> predicate)
        {
            Safelist(name, ctx => new ValueTask<bool>(predicate(ctx)));
        }

        public void Safelist(string name, Func<HttpContext, ValueTask<bool>> predicate)
        {
            Safelists.Add(new Check<bool>(name, predicate));
        }

        public void Blocklist(string name, Func<HttpContext, bool> predicate)
        {
            Blocklist(name, ctx => new ValueTask<bool>(predicate(ctx)));
        }

        public void Blocklist(string name, Func<HttpContext, ValueTask<bool>> predicate)
        {
            Blocklists.Add(new Check<bool>(name, predicate));
        }

        public void Throttle(string name,
            Func<HttpContext, ValueTask<int>> limit,
            Func<HttpContext, ValueTask<TimeSpan>> period,
            Func<HttpContext, ValueTask<string>> discriminator)
        {
            Throttles.Add(new ThrottleCheck(name, limit, period, discriminator));
        }

        [DebuggerStepThrough]
        internal Task<bool> IsSafelisted(HttpContext context)
        {
            return HasMatch(Safelists, context);
        }

        [DebuggerStepThrough]
        internal Task<bool> IsBlocklisted(HttpContext context)
        {
            return HasMatch(Blocklists, context);
        }

        internal async Task<bool> IsThrottled(HttpContext context, IDistributedCache cache)
        {
            foreach (var throttle in Throttles)
            {
                if (await throttle.Matches(context, cache))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<bool> HasMatch(IEnumerable<Check<bool>> checks, HttpContext context)
        {
            foreach (var check in checks)
            {
                if (await check.Predicate.Invoke(context))
                {
                    return true;
                }
            }

            return false;
        }

        private class Check<T>
        {
            public Check(string name, Func<HttpContext, ValueTask<T>> predicate)
            {
                Name = name;
                Predicate = predicate;
            }

            public string Name { get; }

            public Func<HttpContext, ValueTask<T>> Predicate { get; }
        }

        private class ThrottleCheck : Check<string>
        {
            public ThrottleCheck(string name,
                Func<HttpContext, ValueTask<int>> limit,
                Func<HttpContext, ValueTask<TimeSpan>> period,
                Func<HttpContext, ValueTask<string>> discriminator)
                : base(name, discriminator)
            {
                Limit = limit;
                Period = period;
            }

            private Func<HttpContext, ValueTask<int>> Limit { get; }

            private Func<HttpContext, ValueTask<TimeSpan>> Period { get; }

            public async Task<bool> Matches(HttpContext context, IDistributedCache cache)
            {
                var discriminator = await Predicate.Invoke(context);

                if (string.IsNullOrEmpty(discriminator))
                {
                    return false;
                }

                var period = await Period.Invoke(context);
                var limit = await Limit.Invoke(context);

                var key = $"{Name}:{discriminator}";
                var count = await cache.CountAsync(key, period, context.RequestAborted);

                return count > limit;
            }
        }
    }
}
