using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace CityPark.Api.Tests;

/// <summary>
/// TestServer gives every request the same empty remote address, so all clients would share one
/// rate-limit budget. This test-only middleware takes the address from <see cref="HeaderName"/>.
/// </summary>
public sealed class TestClientAddress : IStartupFilter
{
    public const string HeaderName = "X-Test-Client-Address";

    /// <summary>A unique IPv6 address, one per client.</summary>
    public static string New() => new IPAddress(Guid.NewGuid().ToByteArray()).ToString();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[HeaderName], out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            await nextMiddleware(context);
        });
        next(app);
    };
}
