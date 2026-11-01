using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ResumeOwnGroupReadOnly(bool continueInterruptedScenario = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = new GroupFixture(new GroupPreOrderApi(actor.UsingTheApi()));
        actor.Remember(fixture);
        var manifest = GroupTestSettings.Require("AQA_READONLY_MANIFEST");
        if (continueInterruptedScenario) await fixture.ResumeForContinuationAsync(manifest);
        else await fixture.ResumeReadOnlyAsync(manifest);
    }
}
