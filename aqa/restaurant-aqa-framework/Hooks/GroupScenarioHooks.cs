using System.Text.Json;
using Reqnroll.BoDi;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Core;
using Restaurant_Aqa_Framework.Screenplay;

namespace Restaurant_Aqa_Framework.Hooks;

[Binding]
public sealed class GroupScenarioHooks(IObjectContainer container)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [BeforeScenario("requires-waiter", Order = 20000)]
    public async Task RequireActualWaiterAsync()
    {
        var actor = container.Resolve<Actor>();
        var api = new GroupPreOrderApi(actor.UsingTheApi());
        var token = await api.LoginAsync("waiter");
        var profile = (await api.SendAsync("GET", "/me", token)).Require(200);
        if (profile.Json.GetProperty("role").GetString() != "Waiter")
            throw new TUnit.Core.Exceptions.SkipTestException("BLOCKED: configured waiter identity is Customer (/me), Cognito Waiter group absent. Requires owner-authorized identity configuration; tests never change Cognito.");
    }
    [AfterScenario("hw8", Order = -200)]
    public async Task CleanOwnObjectsAsync()
    {
        if (!container.IsRegistered<Actor>()) return;
        var actor = container.Resolve<Actor>();
        if (!actor.TryRecall<GroupFixture>(out _)) return;
        var failures = new List<string>();
        foreach (var fixture in actor.RecallAll<GroupFixture>().Distinct())
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception error) { failures.Add(SecretRedactor.Clean(error.Message)); }
            finally
            {
                Directory.CreateDirectory(ArtifactPaths.Root);
                var path = Path.Combine(ArtifactPaths.Root, fixture.RunId + "-lifecycle.json");
                await File.WriteAllTextAsync(path, SecretRedactor.Clean(JsonSerializer.Serialize(new { fixture.RunId, fixture.Lifecycle, requests = fixture.Api.Diagnostics }, JsonOptions)));
            }
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }
}
