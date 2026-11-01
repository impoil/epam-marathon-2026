using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("a retained group from this run is opened without changing it")]
    public Task ReadOnly() => Actor.AttemptsToAsync(new ResumeOwnGroupReadOnly());

    [Given("an isolated group pre-order in {string} state")]
    public Task Prepare(string state) => Actor.AttemptsToAsync(new PrepareGroupPreOrder(state));

    [When("the owner checks {string} with {string}")]
    public Task Check(string operation, string variant) => Actor.AttemptsToAsync(new ProbeGroupPreOrder(operation, variant: variant));

    [When("the {string} attempts the protected {string} action")]
    public Task Access(string persona, string operation) => Actor.AttemptsToAsync(new ProbeGroupPreOrder("access", persona, operation));

    [When("the owner completes the {string} browser flow")]
    public Task Browse(string flow) => Actor.AttemptsToAsync(new UseGroupPreOrderUi(flow));

    [Then("all group pre-order expectations hold")]
    public async Task Verify()
    {
        var checks = await Actor.AsksForAsync(new TheGroupChecks());
        await Assert.That(checks.Count).IsGreaterThan(0).Because("Every scenario must execute meaningful assertions.");
        await Assert.That(checks.Failures).IsEmpty().Because(string.Join(Environment.NewLine, checks.Failures));
    }
}
