using System.Text.Json;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ProbeGroupPreOrder(string operation, string persona = "owner", string variant = "normal") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var f = actor.Recall<GroupFixture>();
        var checks = new GroupChecks();
        actor.Remember(checks);
        var api = f.Api;
        if (operation == "no group")
        {
            var reply = await api.SendAsync("GET", $"/reservations/{f.Reservation}/invite-token", f.Owner);
            CheckStatus(checks, reply, 404);
            checks.Check(reply.Text.Contains("Group reservation not found", StringComparison.OrdinalIgnoreCase), "Single-person booking must explain that it has no group.");
            return;
        }
        var before = (await api.ReviewAsync(f.Group, f.Owner)).Require(200);
        var token = persona switch { "owner" => f.Owner, "other customer" => f.Other, "waiter" => f.Waiter, "guest" => f.Guests[0].Token, "anonymous" => null, _ => throw new ArgumentException("Unknown persona") };
        var denied = persona is "guest" or "anonymous" ? 401 : 403;
        if (operation == "access")
        {
            GroupReply reply = variant switch
            {
                "review" => await api.ReviewAsync(f.Group, token),
                "submit" => await api.SubmitAsync(f.Group, f.Reservation, token),
                "remove" => await api.RemoveAsync(f.Group, f.Guests[0].Id, token),
                _ => throw new ArgumentException("Unknown protected action")
            };
            CheckStatus(checks, reply, denied);
            checks.Check(Snapshot(before.Json) == Snapshot((await api.ReviewAsync(f.Group, f.Owner)).Require(200).Json), "Denied action changed participants, dishes, flags or total.");
            var status = (await api.SendAsync("GET", $"/guest/groups/{f.Group}/status", f.Owner)).Require(200);
            checks.Check(status.Json.GetProperty("status").GetString() == "Open", "Denied action changed group status.");
            return;
        }
        if (operation == "review")
        {
            CheckSnapshot(checks, before.Json, f);
            if (variant == "changed cart")
            {
                var guest = f.Guests[0];
                // This scenario prepares draft carts; the change is a real public guest action.
                await f.AddAsync(guest, quantity: 1);
                var after = await api.PollAsync(() => api.ReviewAsync(f.Group, f.Owner), r => r.Status == 200 && r.Json.GetProperty("total").GetDecimal() == before.Json.GetProperty("total").GetDecimal() + f.Dishes[0].Price);
                checks.Check(after.Status == 200 && after.Json.GetProperty("total").GetDecimal() == before.Json.GetProperty("total").GetDecimal() + f.Dishes[0].Price, "Changing draft cart did not update review total by one dish price.");
            }
            return;
        }
        if (operation == "missing review")
        {
            CheckStatus(checks, await api.ReviewAsync(Guid.NewGuid().ToString(), f.Owner), 404);
            return;
        }
        if (operation is "submit" or "waiter visibility")
        {
            var group = variant == "missing group" ? Guid.NewGuid().ToString() : f.Group;
            var reservation = variant == "missing reservation" ? Guid.NewGuid().ToString() : variant == "invalid reservation" ? " " : f.Reservation;
            var reply = await api.SubmitAsync(group, reservation, f.Owner);
            if (variant != "success")
            {
                var expected = variant is "missing group" or "missing reservation" ? 404 : 400;
                CheckStatus(checks, reply, expected);
                var message = variant switch { "no submitted carts" => "no submitted guest sessions", "no dishes" => "no dishes", "missing order" => "Order not found", _ => "" };
                checks.Check(message.Length == 0 || reply.Text.Contains(message, StringComparison.OrdinalIgnoreCase), $"Expected refusal message '{message}', received {reply.Text}");
                checks.Check(Snapshot(before.Json) == Snapshot((await api.ReviewAsync(f.Group, f.Owner)).Require(200).Json), "Rejected submit changed the review snapshot.");
                var stillOpen = (await api.SendAsync("GET", $"/guest/groups/{f.Group}/status", f.Owner)).Require(200);
                checks.Check(stillOpen.Json.GetProperty("status").GetString() == "Open", "Rejected submit changed group status.");
                return;
            }
            CheckStatus(checks, reply, 200);
            f.FinalSubmitted = reply.Status == 200;
            await CheckFinalAsync(checks, f, before.Json);
            if (operation == "waiter visibility")
            {
                var waiterOrder = await api.SendAsync("GET", $"/orders/{f.Reservation}", f.Waiter);
                CheckStatus(checks, waiterOrder, 200);
                if (waiterOrder.Status == 200)
                {
                    var ownerOrder = (await api.SendAsync("GET", $"/orders/{f.Reservation}", f.Owner)).Require(200);
                    checks.Check(ownerOrder.Json.GetProperty("orderItems").ToString() == waiterOrder.Json.GetProperty("orderItems").ToString(), "Waiter and Owner see different consolidated order items.");
                }
                return;
            }
            var repeated = await api.SubmitAsync(f.Group, f.Reservation, f.Owner);
            CheckStatus(checks, repeated, 400);
            checks.Check(repeated.Text.Contains("GroupReservationSubmitted", StringComparison.Ordinal), "Repeated submit must explain the submitted state.");
            await CheckFinalAsync(checks, f, before.Json);
            return;
        }
        if (operation == "remove")
        {
            var id = variant == "owner" ? f.OwnerSession : variant == "missing" ? Guid.NewGuid().ToString() : f.Guests[0].Id;
            GroupFixture? foreign = null;
            try
            {
                if (variant == "foreign")
                {
                    foreign = new GroupFixture(api);
                    actor.Remember("foreign", foreign);
                    await foreign.CreateAsync();
                    id = (await foreign.JoinAsync("Foreign")).Id;
                }
                var foreignBefore = foreign is null ? null : Snapshot((await api.ReviewAsync(foreign.Group, foreign.Owner)).Require(200).Json);
                var reply = await api.RemoveAsync(f.Group, id, f.Owner);
                if (variant is "owner" or "missing" or "foreign")
                {
                    CheckStatus(checks, reply, variant == "missing" ? 404 : 400);
                    checks.Check(Snapshot(before.Json) == Snapshot((await api.ReviewAsync(f.Group, f.Owner)).Require(200).Json), "Rejected remove changed source group.");
                    if (foreign is not null) checks.Check(foreignBefore == Snapshot((await api.ReviewAsync(foreign.Group, foreign.Owner)).Require(200).Json), "Cross-group remove changed target group.");
                    return;
                }
                CheckStatus(checks, reply, 200);
                var deleted = before.Json.GetProperty("participants").EnumerateArray().Single(p => p.GetProperty("id").GetString() == id);
                checks.Check(reply.Json.GetProperty("total").GetDecimal() == before.Json.GetProperty("total").GetDecimal() - deleted.GetProperty("total").GetDecimal(), "Remove did not subtract exactly the removed subtotal.");
                checks.Check(!reply.Json.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("id").GetString() == id), "Removed guest remains in immediate response.");
                checks.Check(reply.Json.GetProperty("participants").GetArrayLength() == before.Json.GetProperty("participants").GetArrayLength() - 1, "Remove changed another participant.");
                var after = await api.PollAsync(() => api.ReviewAsync(f.Group, f.Owner), r => r.Status == 200 && !r.Json.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("id").GetString() == id));
                checks.Check(Snapshot(after.Json) == Snapshot(reply.Json), "Persisted review differs from remove response.");
                CheckStatus(checks, await api.RemoveAsync(f.Group, id, f.Owner), 404);
                CheckStatus(checks, await api.SendAsync("GET", $"/guest/sessions/{id}", f.Guests[0].Token), 401);
                checks.Check(after.Json.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("type").GetString() == "Owner"), "Remove lost the Owner.");
            }
            finally { /* Lifecycle cleanup is centralized in GroupScenarioHooks, including partial setup. */ }
        }
    }

    public static void CheckStatus(GroupChecks checks, GroupReply reply, int expected) => checks.Check(reply.Status == expected, $"Expected {expected}, received {reply.Status}: {reply.Text}");

    public static void CheckSnapshot(GroupChecks checks, JsonElement review, GroupFixture f)
    {
        var participants = review.GetProperty("participants").EnumerateArray().ToArray();
        checks.Check(participants.Count(p => p.GetProperty("type").GetString() == "Owner") == 1, "Exactly one Owner session must be present.");
        checks.Check(participants.Length == 1 + f.Guests.Count, "Participant count differs from own created sessions.");
        foreach (var guest in f.Guests) checks.Check(participants.Any(p => p.GetProperty("id").GetString() == guest.Id && p.GetProperty("name").GetString() == guest.Name && p.GetProperty("type").GetString() == "Guest"), "Missing or misidentified Guest.");
        decimal sum = 0;
        foreach (var participant in participants)
        {
            if (f.ExpectedCarts.TryGetValue(participant.GetProperty("id").GetString()!, out var cart))
            {
                var actualItems = participant.GetProperty("items").EnumerateArray().ToArray();
                checks.Check(participant.GetProperty("isSubmitted").GetBoolean() == cart.Submitted, "Cart submitted/draft flag differs from the actions performed.");
                checks.Check(actualItems.Length == cart.Quantities.Count && actualItems.All(i => cart.Quantities.TryGetValue(i.GetProperty("dish").GetProperty("id").GetString()!, out var quantity) && quantity == i.GetProperty("quantity").GetInt32()), "Cart dishes or quantities differ from the actions performed.");
                checks.Check(actualItems.All(i => f.Dishes.Any(d => d.Id == i.GetProperty("dish").GetProperty("id").GetString() && d.Price == i.GetProperty("dish").GetProperty("price").GetDecimal() && d.Name == i.GetProperty("dish").GetProperty("name").GetString())), "Review dish name/price differs from the independently read live catalog.");
            }
            var subtotal = participant.GetProperty("items").EnumerateArray().Sum(i => i.GetProperty("dish").GetProperty("price").GetDecimal() * i.GetProperty("quantity").GetInt32());
            checks.Check(subtotal == participant.GetProperty("total").GetDecimal(), "Subtotal differs from dish prices times quantities.");
            sum += subtotal;
        }
        checks.Check(sum == review.GetProperty("total").GetDecimal(), "Review total differs from all participant subtotals (including drafts).");
    }

    public static async Task CheckFinalAsync(GroupChecks checks, GroupFixture f, JsonElement review)
    {
        var expected = review.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("isSubmitted").GetBoolean())
            .SelectMany(p => p.GetProperty("items").EnumerateArray()).GroupBy(i => i.GetProperty("dish").GetProperty("id").GetString()!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.GetProperty("quantity").GetInt32()), StringComparer.Ordinal);
        var group = (await f.Api.SendAsync("GET", $"/guest/groups/{f.Group}/status", f.Owner)).Require(200);
        checks.Check(group.Json.GetProperty("status").GetString() == "Submitted", "Group is not Submitted.");
        foreach (var token in new[] { f.Owner })
        {
            var order = await f.Api.PollAsync(() => f.Api.SendAsync("GET", $"/orders/{f.Reservation}", token), r => r.Status == 200 && IsSubmittedOrder(r.Json.GetProperty("status")));
            CheckStatus(checks, order, 200);
            if (order.Status != 200) continue;
            checks.Check(IsSubmittedOrder(order.Json.GetProperty("status")), "Owner/waiter order status differs from SubmittedByCustomer (wire value 3).");
            var items = order.Json.GetProperty("orderItems").EnumerateArray().ToArray();
            checks.Check(items.Length == expected.Count, "Consolidated order has missing, duplicate or draft dishes.");
            checks.Check(items.All(i => expected.TryGetValue(i.GetProperty("dish").GetProperty("id").GetString()!, out var q) && q == i.GetProperty("quantity").GetInt32()), "Consolidated quantities differ from submitted carts.");
            var total = items.Sum(i => i.GetProperty("dish").GetProperty("price").GetDecimal() * i.GetProperty("quantity").GetInt32());
            var expectedTotal = review.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("isSubmitted").GetBoolean()).Sum(p => p.GetProperty("total").GetDecimal());
            checks.Check(total == expectedTotal, "Final order total includes draft/removed dishes or incorrect prices.");
        }
        var edit = await f.Api.SendAsync("PUT", $"/orders/{f.Reservation}", f.Owner, new { orderItems = new[] { new { dishId = f.Dishes[0].Id, quantity = 99 } } });
        CheckStatus(checks, edit, 400);
        if (f.Guests.Count > 0) CheckStatus(checks, await f.Api.RemoveAsync(f.Group, f.Guests[0].Id, f.Owner), 400);
    }

    private static string Snapshot(JsonElement review) => string.Join("|", review.GetProperty("participants").EnumerateArray().OrderBy(p => p.GetProperty("id").GetString(), StringComparer.Ordinal).Select(p => p.ToString())) + review.GetProperty("total").ToString();

    private static bool IsSubmittedOrder(JsonElement status) => status.ValueKind == JsonValueKind.Number
        ? status.GetInt32() == (int)Api.Dtos.Orders.OrderStatus.SubmittedByCustomer
        : status.GetString() == "SubmittedByCustomer";
}
