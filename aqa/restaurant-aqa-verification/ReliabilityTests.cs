using System.Text.Json;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace RestaurantAqaVerification;

[Category("offline")]
public sealed class ReliabilityTests
{
    [Test]
    [Arguments("POST", "/reservations")]
    [Arguments("POST", "/guest/sessions")]
    [Arguments("POST", "/orders")]
    [Arguments("POST", "/group-reservations/g/submit")]
    [Arguments("DELETE", "/guest/groups/g/sessions/s")]
    [Arguments("DELETE", "/reservations")]
    [Arguments("PUT", "/orders/r")]
    [Arguments("PATCH", "/reservations/r/status")]
    public async Task BusinessRequestsStopBeforeTransport(string method, string path)
    {
        // Null transport is intentional: a missing or late guard fails this test, with no network possible.
        var api = new GroupPreOrderApi(null!);
        var blocked = false;
        try { await api.SendAsync(method, path); }
        catch (InvalidOperationException error) { blocked = error.Message.StartsWith("BLOCKED:", StringComparison.Ordinal); }
        await Assert.That(blocked).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("35")]
    [Arguments("5")]
    [Arguments("-1")]
    [Arguments("bad")]
    [Arguments(" 4")]
    public async Task MissingInvalidOrOldReservationBudgetFailsClosed(string? value)
    {
        var blocked = false;
        try { BusinessWriteSafety.ParseReservationLimit(value); }
        catch (InvalidOperationException) { blocked = true; }
        await Assert.That(blocked).IsTrue();
    }

    [Test]
    [Arguments("0", 0)]
    [Arguments("4", 4)]
    public async Task BoundedBudgetParsesWithoutEnablingWrites(string value, int expected)
    {
        await Assert.That(BusinessWriteSafety.ParseReservationLimit(value)).IsEqualTo(expected);
        await Assert.That(BusinessWriteSafety.IsSuspended).IsTrue();
    }

    [Test]
    [Arguments(true, "Open", false, CleanupDisposition.RetainSubmitted)]
    [Arguments(false, "Submitted", false, CleanupDisposition.RetainSubmitted)]
    [Arguments(false, "Open", true, CleanupDisposition.RetainUncertainSubmission)]
    [Arguments(false, "Open", false, CleanupDisposition.DeleteOwnGuests)]
    [Arguments(false, "Closed", false, CleanupDisposition.RetainClosed)]
    public async Task CleanupNeverDeletesAfterConfirmedOrUncertainSubmit(bool confirmed, string status, bool attempted, CleanupDisposition expected) =>
        await Assert.That(GroupLifecycle.Decide(confirmed, status, attempted)).IsEqualTo(expected);

    [Test]
    public async Task UnknownCleanupStateFailsClosed()
    {
        var blocked = false;
        try { GroupLifecycle.Decide(false, "unexpected"); }
        catch (InvalidOperationException) { blocked = true; }
        await Assert.That(blocked).IsTrue();
    }

    private const string Review = """
        {"participants":[
          {"id":"owner","type":"Owner","name":"Owner","isSubmitted":true,"total":10,"items":[{"dish":{"id":"a","name":"A","price":10},"quantity":1}]},
          {"id":"guest","type":"Guest","name":"Guest","isSubmitted":true,"total":10,"items":[{"dish":{"id":"b","name":"B","price":10},"quantity":1}]},
          {"id":"draft","type":"Guest","name":"Draft","isSubmitted":false,"total":70,"items":[{"dish":{"id":"c","name":"C","price":10},"quantity":7}]}
        ],"total":90}
        """;

    [Test]
    [Arguments("duplicate")]
    [Arguments("missing")]
    [Arguments("draft")]
    [Arguments("quantity")]
    [Arguments("total")]
    [Arguments("balanced-price")]
    [Arguments("name")]
    [Arguments("correct")]
    public async Task FinalAssertionsRejectCorruptOrdersIncludingSamePriceDuplicates(string variant)
    {
        var rows = new List<object> { Item("a", 1), Item("b", 1) };
        if (variant == "duplicate") rows[1] = Item("a", 1); // same length, quantities AND total as correct order
        if (variant == "missing") rows.RemoveAt(1);
        if (variant == "draft") rows.Add(Item("c", 7));
        if (variant == "quantity") rows[0] = Item("a", 2);
        if (variant == "total") rows[0] = Item("a", 1, 11);
        if (variant == "balanced-price") { rows[0] = Item("a", 1, 9); rows[1] = Item("b", 1, 11); }
        if (variant == "name") rows[0] = Item("a", 1, name: "Unexpected");
        var checks = new GroupChecks();
        GroupOrderAssertions.CheckOrder(checks, JsonSerializer.SerializeToElement(new { orderItems = rows }), JsonSerializer.Deserialize<JsonElement>(Review));
        await Assert.That(checks.Failures.Count == 0).IsEqualTo(variant == "correct");
    }

    [Test]
    public async Task RemoveCannotSilentlyChangeASurvivingCart()
    {
        var before = JsonSerializer.Deserialize<JsonElement>(Review);
        var survivors = before.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("id").GetString() != "guest").ToArray();
        var correct = JsonSerializer.SerializeToElement(new { participants = survivors, total = 80 });
        var checks = new GroupChecks();
        GroupOrderAssertions.CheckRemoval(checks, before, correct, "guest");
        await Assert.That(checks.Failures).IsEmpty();
        var corrupt = JsonSerializer.Deserialize<JsonElement>(correct.GetRawText().Replace("\"quantity\":1", "\"quantity\":2", StringComparison.Ordinal));
        GroupOrderAssertions.CheckRemoval(checks, before, corrupt, "guest");
        await Assert.That(checks.Failures.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task IncompleteSuccessfulReadTimesOutInsteadOfReturningFalseReady()
    {
        var api = new GroupPreOrderApi(null!);
        var timedOut = false;
        try { await api.PollAsync(() => Task.FromResult(new GroupReply(200, JsonSerializer.SerializeToElement(new { ready = false }))), r => r.Json.GetProperty("ready").GetBoolean(), timeout: TimeSpan.FromMilliseconds(20)); }
        catch (TimeoutException) { timedOut = true; }
        await Assert.That(timedOut).IsTrue();
    }

    [Test]
    public async Task ReadConsistencyCanWaitForNewOrderWithoutRetryingAMutation()
    {
        var reads = 0;
        var api = new GroupPreOrderApi(null!);
        var response = await api.PollAsync(() => Task.FromResult(new GroupReply(++reads == 1 ? 404 : 200, JsonSerializer.SerializeToElement(new { }))), r => r.Status == 200, allowNotFound: true);
        await Assert.That(response.Status).IsEqualTo(200);
        await Assert.That(reads).IsEqualTo(2);
    }

    private static object Item(string id, int quantity, decimal price = 10, string? name = null) => new { dish = new { id, name = name ?? id.ToUpperInvariant(), price }, quantity };
}
