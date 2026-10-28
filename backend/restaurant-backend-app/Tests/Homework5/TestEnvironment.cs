using System.Text.Json;

namespace APIHandler.Tests.Homework5;

internal static class TestEnvironment
{
    public static readonly string BackendRoot = FindBackendRoot();
    private static readonly object Gate = new();
    private static bool initialized;

    public static void Initialize()
    {
        lock (Gate)
        {
            if (initialized) return;
            using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(BackendRoot,
                "dnapp/lambdas/api-handler/lambda_config.json")));
            foreach (var variable in config.RootElement.GetProperty("env_variables").EnumerateObject())
                Environment.SetEnvironmentVariable(variable.Name, "hw5-" + variable.Name);
            Environment.SetEnvironmentVariable("REGION", "eu-north-1");
            Environment.SetEnvironmentVariable("groupReservationsTable", "hw5-GroupReservations");
            Environment.SetEnvironmentVariable("ordersTable", "hw5-Orders");
            initialized = true;
        }
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "deployment_resources.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Cannot find backend deployment resources.");
    }
}
