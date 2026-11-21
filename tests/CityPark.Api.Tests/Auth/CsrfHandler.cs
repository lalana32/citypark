using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Auth;

using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

/// <summary>
/// Does what the SPA does: before every unsafe request it gets a fresh token from GET /auth/csrf
/// with the request's own cookies and sends it in the X-XSRF-TOKEN header.
/// </summary>
internal sealed class CsrfHandler : DelegatingHandler
{
    /// <summary>Set on a request to send it exactly as built, for tests of the CSRF check itself.</summary>
    public static readonly HttpRequestOptionsKey<bool> Skip = new("CityPark.SkipCsrf");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsSafe(request.Method) || (request.Options.TryGetValue(Skip, out var skip) && skip))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // The token is bound to the signed-in user, so it is fetched with the same session cookie.
        using var csrfRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri!, AuthClient.Csrf));
        var cookies = request.Headers.TryGetValues(HeaderNames.Cookie, out var sent) ? sent.ToList() : [];
        if (cookies.Count > 0)
        {
            csrfRequest.Headers.TryAddWithoutValidation(HeaderNames.Cookie, string.Join("; ", cookies));
        }

        using var csrfResponse = await base.SendAsync(csrfRequest, cancellationToken);
        csrfResponse.EnsureSuccessStatusCode();
        var token = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("token").GetString();

        // No Set-Cookie when the request already carried a valid antiforgery cookie.
        if (csrfResponse.Headers.TryGetValues(HeaderNames.SetCookie, out var setCookies)
            && SetCookieHeaderValue.ParseList([.. setCookies]).FirstOrDefault(c => c.Name == CsrfProtection.CookieName) is { } cookie)
        {
            cookies.Add($"{cookie.Name}={cookie.Value}");
            request.Headers.Remove(HeaderNames.Cookie);
            request.Headers.TryAddWithoutValidation(HeaderNames.Cookie, string.Join("; ", cookies));
        }

        request.Headers.Add(CsrfProtection.HeaderName, token);
        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsSafe(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options || method == HttpMethod.Trace;
}
