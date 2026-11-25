using CityPark.Api.Auth;

using Microsoft.AspNetCore.Hosting;

namespace CityPark.Api.Tests.Auth;

/// <summary>Production with Cookies:AllowHttp, as in the Compose app profile behind nginx on plain HTTP.</summary>
public sealed class HttpCookiesApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(CookieSecurity.AllowHttpKey, "true");
    }
}
