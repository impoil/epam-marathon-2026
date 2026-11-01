namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class GroupChecks
{
    public List<string> Failures { get; } = [];
    public int Count { get; private set; }
    public void Check(bool matches, string reason)
    {
        Count++;
        if (!matches) Failures.Add(reason);
    }
}
