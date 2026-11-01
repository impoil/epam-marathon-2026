using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Configuration;

namespace RestaurantAqaVerification;

[Category("offline-browser")]
public sealed class BrowserSafetyTests
{
    [Test]
    public async Task BrowserBlocksBusinessWritesAndCrossOriginSignInBeforeNetwork()
    {
        const string origin = "http://127.0.0.1:18457";
        Environment.SetEnvironmentVariable("API_URL", origin + "/demo");
        Environment.SetEnvironmentVariable("UI_URL", origin);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHANNEL") ?? "chrome" });
        await using var context = await browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block });
        var reachedSink = new List<string>();
        // This is a local synthetic transport sink, not the Green&Tasty application or server.
        await context.RouteAsync("**/*", route =>
        {
            reachedSink.Add(route.Request.Method + " " + new Uri(route.Request.Url).AbsolutePath);
            return route.FulfillAsync(new() { ContentType = "text/html", Body = "<html><body>offline sink</body></html>" });
        });
        await BusinessWriteSafety.ProtectBrowserAsync(context);
        var page = await context.NewPageAsync();
        await page.GotoAsync(origin);
        var allowed = await page.EvaluateAsync<bool>("async () => (await fetch('/demo/info')).ok && (await fetch('/demo/signin', {method:'POST'})).ok");
        var denied = await page.EvaluateAsync<bool>("async () => { try { await fetch('/demo/orders', {method:'POST'}); return false; } catch { return true; } }");
        var crossOriginDenied = await page.EvaluateAsync<bool>("async () => { try { await fetch('http://127.0.0.1:18458/demo/signin', {method:'POST'}); return false; } catch { return true; } }");
        await Assert.That(allowed && denied && crossOriginDenied).IsTrue();
        await Assert.That(reachedSink.Count(r => r == "POST /demo/signin")).IsEqualTo(1);
        await Assert.That(reachedSink.Any(r => r.Contains("orders", StringComparison.Ordinal))).IsFalse();
    }
}
