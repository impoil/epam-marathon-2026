using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ResumeOwnGroupReadOnly : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = new GroupFixture(new GroupPreOrderApi(actor.UsingTheApi()));
        actor.Remember(fixture);
        await fixture.ResumeReadOnlyAsync(GroupTestSettings.Require("AQA_READONLY_MANIFEST"));
    }
}
