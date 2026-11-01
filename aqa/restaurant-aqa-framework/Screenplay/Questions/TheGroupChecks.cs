namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheGroupChecks : IQuestion<GroupChecks>
{
    public Task<GroupChecks> AnsweredByAsync(Actor actor) => Task.FromResult(actor.Recall<GroupChecks>());
}
