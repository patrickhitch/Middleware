using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Hellang.Middleware.RateLimiting
{
    public class RateLimitingMiddleware
    {
        public RateLimitingMiddleware(RequestDelegate next, IDistributedCache cache, IOptions<RateLimitingOptions> options)
        {
            Next = next;
            Cache = cache;
            Options = options.Value;
        }

        private RequestDelegate Next { get; }
        
        private IDistributedCache Cache { get; }
        
        private RateLimitingOptions Options { get; }

        public async Task Invoke(HttpContext context)
        {
            if (await Options.IsSafelisted(context))
            {
                await Next(context);
                return;
            }

            if (await Options.IsBlocklisted(context))
            {
                await Block(context);
                return;
            }

            if (await Options.IsThrottled(context, Cache))
            {
                await Throttle(context);
                return;
            }

            await Next(context);
        }

        private Task Block(HttpContext context)
        {
            throw new System.NotImplementedException();
        }

        private Task Throttle(HttpContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}
