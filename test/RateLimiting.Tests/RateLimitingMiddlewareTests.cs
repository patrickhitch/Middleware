using System;
using System.Net;
using System.Threading.Tasks;
using Hellang.Middleware.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RateLimiting.Tests
{
    public class RateLimitingMiddlewareTests
    {
        [Fact]
        public async Task Test()
        {
            void Configure(RateLimitingOptions options)
            {
                options.Blocklist("localhost admin", x => x.Request.Path.StartsWithSegments("/admin") && !x.Connection.RemoteIpAddress.Equals(IPAddress.Loopback));
            }

            using (var server = CreateServer(Configure))
            using (var client = server.CreateClient())
            using (var response = await client.GetAsync("/admin"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        private static TestServer CreateServer(Action<RateLimitingOptions> configure)
        {
            var builder = new WebHostBuilder()
                .UseEnvironment(EnvironmentName.Development)
                .ConfigureServices(x => x
                    .AddDistributedMemoryCache()
                    .AddRateLimiting(configure))
                .Configure(x => x
                    .UseRemoteAddress(IPAddress.Loopback)
                    .UseRateLimiting());

            return new TestServer(builder);
        }
    }
}
