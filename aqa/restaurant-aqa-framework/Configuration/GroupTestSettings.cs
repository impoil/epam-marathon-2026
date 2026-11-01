namespace Restaurant_Aqa_Framework.Configuration;

public static class GroupTestSettings
{
    public static string[] BrowserArguments()
    {
        var address = Environment.GetEnvironmentVariable("AQA_UI_DNS_ADDRESS");
        if (string.IsNullOrEmpty(address)) return [];
        if (!System.Net.IPAddress.TryParse(address, out _)) throw new InvalidOperationException("AQA_UI_DNS_ADDRESS must be a validated public DNS answer.");
        var host = new Uri(Settings.Current.UiBaseUrl).DnsSafeHost;
        // Process-local DNS override only. HTTPS hostname, SNI, origin and certificate validation stay enabled.
        return [$"--host-resolver-rules=MAP {host} {address},EXCLUDE localhost"];
    }
    public static string Require(string name)
    {
        EnvConfig.EnsureLoaded();
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value : throw new TUnit.Core.Exceptions.SkipTestException($"BLOCKED: configure {name}; no identities are provisioned by tests.");
    }

    public static void RequireWrites()
    {
        if (Require("AQA_ALLOW_WRITES") != "true")
            throw new InvalidOperationException("Set AQA_ALLOW_WRITES=true to create isolated test reservations.");
        if (Environment.GetEnvironmentVariable("AQA_CANCEL_RESERVATIONS") != "true" && Require("AQA_RETAIN_TEST_DATA") != "true")
            throw new InvalidOperationException("This deployment has no reservation cleanup route. Explicitly opt into AQA_RETAIN_TEST_DATA=true; see the lifecycle documentation.");
    }
}
