using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class PrepareGroupPreOrder(string state) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        if (state is not ("empty" or "review" or "ready" or "draft" or "no-group" or "missing-order" or "empty-submitted" or "zero-participants"))
            throw new InvalidOperationException("Unknown fixture state; no setup was attempted.");
        if (state is "empty-submitted" or "zero-participants")
            throw new TUnit.Core.Exceptions.SkipTestException(state == "empty-submitted"
                ? "BLOCKED: public guest submit rejects empty carts; no safe API can create a submitted empty cart. Requires isolated fixture API; no DynamoDB writes."
                : "BLOCKED: every new group gets an Owner session and removing Owner is forbidden; zero participants cannot be created through public APIs.");
        var fixture = new GroupFixture(new GroupPreOrderApi(actor.UsingTheApi()));
        actor.Remember(fixture); // registered before first write, including partially failed setup
        await fixture.CreateAsync(state == "no-group" ? 1 : 4);
        if (state == "no-group") return;
        if (state == "empty") return;
        if (state is "review" or "ready" or "draft") await fixture.OwnerCartAsync(state != "draft");
        var guest = await fixture.JoinAsync("Guest-A");
        if (state != "empty-submitted") await fixture.AddAsync(guest, quantity: 2);
        if (state != "draft") await fixture.SubmitGuestAsync(guest);
        if (state is "review" or "ready")
        {
            var draft = await fixture.JoinAsync("Guest-B");
            if (state == "ready") await fixture.AddAsync(draft, 1, 7);
        }
        // Make state ready through reads, rather than delaying/repeating a mutation.
        await fixture.Api.PollAsync(() => fixture.Api.ReviewAsync(fixture.Group, fixture.Owner), r =>
            r.Status == 200 && r.Json.GetProperty("participants").EnumerateArray()
                .Any(p => p.GetProperty("id").GetString() == guest.Id && p.GetProperty("isSubmitted").GetBoolean() == (state != "draft")));
    }
}
