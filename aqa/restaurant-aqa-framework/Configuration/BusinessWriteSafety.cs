using System.Globalization;
using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Configuration;

/// <summary>Temporary code-level stop until an isolated environment and cleanup protocol are approved.</summary>
public static class BusinessWriteSafety
{
    public static bool IsSuspended => true;

    public static bool IsReadOrSignIn(string method, string path) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase) || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ||
        (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.Equals("/signin", StringComparison.Ordinal));

    public static void RequireEnabled()
    {
        if (IsSuspended)
            throw new InvalidOperationException("BLOCKED: business writes are suspended pending approved isolated fixtures/cleanup. Environment flags cannot override this stop.");
    }

    public static void CheckRequest(string method, string path)
    {
        if (!IsReadOrSignIn(method, path)) RequireEnabled();
    }

    public static int ParseReservationLimit(string? raw)
    {
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit is < 0 or > 4)
            throw new InvalidOperationException("AQA_MAX_RESERVATIONS must be an explicit integer from 0 to 4. The previous budget 35 is suspended.");
        return limit;
    }

    public static Task ProtectBrowserAsync(IBrowserContext context) => context.RouteAsync("**/*", async route =>
    {
        var request = route.Request;
        // Apply to all origins: a redirect or misconfigured frontend must not bypass the API guard.
        var requestUri = new Uri(request.Url);
        var api = new Uri(Settings.Current.ApiBaseUrl);
        var signIn = requestUri.GetLeftPart(UriPartial.Authority) == api.GetLeftPart(UriPartial.Authority) &&
            requestUri.AbsolutePath == api.AbsolutePath.TrimEnd('/') + "/signin" && requestUri.Query.Length == 0;
        if (request.Method is "GET" or "HEAD" || (signIn && request.Method == "POST")) await route.FallbackAsync();
        else await route.AbortAsync("blockedbyclient");
    });
}
