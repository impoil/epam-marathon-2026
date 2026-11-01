using System.Globalization;
using System.Text.Json;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;

namespace Restaurant_Aqa_Framework.Api.Seeding;

public sealed record TestGuest(string Id, string Token, string Name);
public sealed record TestDish(string Id, string Name, decimal Price);
public sealed class ExpectedCart
{
    public bool Submitted { get; set; }
    public Dictionary<string, int> Quantities { get; } = new(StringComparer.Ordinal);
}

/// <summary>Owns only IDs returned by this scenario's successful creates. Never scans/deletes shared data.</summary>
public sealed class GroupFixture(GroupPreOrderApi api) : IAsyncDisposable
{
    public string RunId { get; private set; } = $"AQA-HW8-{Guid.NewGuid():N}";
    public bool ReadOnly { get; private set; }
    public bool IsContinuation { get; private set; }
    public GroupPreOrderApi Api { get; } = api;
    public string Owner { get; private set; } = "";
    public string Other { get; private set; } = "";
    public string Waiter { get; private set; } = "";
    public string Reservation { get; private set; } = "";
    public string Group { get; private set; } = "";
    public string Invite { get; private set; } = "";
    public string OwnerSession { get; private set; } = "";
    public string Location { get; private set; } = "";
    public List<TestDish> Dishes { get; } = [];
    public List<TestGuest> Guests { get; } = [];
    public Dictionary<string, ExpectedCart> ExpectedCarts { get; } = new(StringComparer.Ordinal);
    private bool finalSubmitted;
    private bool submissionAttempted;
    public bool FinalSubmitted { get => finalSubmitted; set => finalSubmitted |= value; }
    public bool SubmissionAttempted { get => submissionAttempted; set => submissionAttempted |= value; }
    public List<object> Lifecycle { get; } = [];

    public async Task ResumeReadOnlyAsync(string manifest)
    {
        ReadOnly = true;
        var data = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(manifest));
        RunId = data.GetProperty(nameof(RunId)).GetString()!;
        if (!RunId.StartsWith("AQA-HW8-", StringComparison.Ordinal)) throw new InvalidOperationException("Only a HW8 lifecycle manifest may be resumed.");
        Reservation = data.GetProperty(nameof(Lifecycle)).EnumerateArray().First(p => p.GetProperty("action").GetString() == "created").GetProperty("reservationId").GetString()!;
        Owner = await Api.LoginAsync("owner");
        var invite = (await Api.SendAsync("GET", $"/reservations/{Reservation}/invite-token", Owner)).Require(200);
        Group = invite.Json.GetProperty("invite").GetProperty("GroupReservationId").GetString()!;
        Invite = invite.Json.GetProperty("token").GetString()!;
        var review = (await Api.ReviewAsync(Group, Owner)).Require(200);
        foreach (var p in review.Json.GetProperty("participants").EnumerateArray())
            if (p.GetProperty("type").GetString() == "Guest") Guests.Add(new TestGuest(p.GetProperty("id").GetString()!, "", p.GetProperty("name").GetString()!));
        Lifecycle.Add(new { action = "read-only-verification", reservationId = Reservation, sourceManifest = Path.GetFileName(manifest) });
    }

    public async Task CreateAsync(int guestCount = 4)
    {
        GroupTestSettings.RequireWrites();
        Owner = await Api.LoginAsync("owner");
        // Check optional roles before creating data so missing identities cannot strand a booking.
        Other = await Api.LoginAsync("other");
        Waiter = await Api.LoginAsync("waiter");
        var table = GroupTestSettings.Require("TESTTABLE_ID");
        var locations = (await Api.SendAsync("GET", "/locations")).Require(200).Json;
        foreach (var location in locations.EnumerateArray())
        {
            var id = location.GetProperty("id").GetString()!;
            var date = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(365).AddHours(12).ToString("O", CultureInfo.InvariantCulture));
            var tables = (await Api.SendAsync("GET", $"/available-tables?locationId={id}&capacity={guestCount}&startTime={date}")).Require(200).Json;
            if (tables.EnumerateArray().Any(t => t.GetProperty("id").GetString() == table)) { Location = id; break; }
        }
        if (Location.Length == 0) throw new InvalidOperationException("Configured table is not in the live catalog.");
        var dishes = (await Api.SendAsync("GET", $"/dishes?locationId={Location}")).Require(200).Json;
        Dishes.AddRange(dishes.EnumerateArray().Where(d => d.GetProperty("state").GetString() == "Available")
            .Select(d => new TestDish(d.GetProperty("id").GetString()!, d.GetProperty("name").GetString()!, d.GetProperty("price").GetDecimal())).Take(2));
        if (Dishes.Count < 2) throw new InvalidOperationException("Two available dishes are required; no catalog data is created.");
        // Random future date/slot; API availability is checked before the single POST.
        DateTime start = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(7, 60)).AddHours(10);
            var slots = (await Api.SendAsync("GET", $"/tables/{table}/available-time-slots?startTime={Uri.EscapeDataString(candidate.ToString("O", CultureInfo.InvariantCulture))}")).Require(200).Json.EnumerateArray().ToArray();
            if (slots.Length == 0) continue;
            start = DateTime.SpecifyKind(slots[Random.Shared.Next(slots.Length)].GetProperty("start").GetDateTime(), DateTimeKind.Utc);
            break;
        }
        if (start == default) throw new InvalidOperationException("No isolated future slot found; no booking was created.");
        var reservation = (await Api.SendAsync("POST", "/reservations", Owner, new { tableId = table, startTime = start, endTime = start.AddMinutes(90), guestCount })).Require(201);
        Reservation = reservation.Json.GetProperty("id").GetString()!;
        Lifecycle.Add(new { RunId, reservationId = Reservation, start, action = "created" });
        if (guestCount == 1) return;
        var invitation = await Api.PollAsync(() => Api.SendAsync("GET", $"/reservations/{Reservation}/invite-token", Owner), r => r.Status == 200);
        invitation.Require(200);
        Invite = invitation.Json.GetProperty("token").GetString()!;
        SecretRedactor.Add(Invite);
        Group = invitation.Json.GetProperty("invite").GetProperty("GroupReservationId").GetString()!;
        OwnerSession = (await Api.SendAsync("GET", $"/reservations/{Reservation}/guest-session-id", Owner)).Require(200).Json.GetProperty("guestSessionId").GetString()!;
        ExpectedCarts[OwnerSession] = new ExpectedCart();
        await ReviewReadyAsync(1);
    }

    public async Task ResumeForContinuationAsync(string manifest)
    {
        GroupTestSettings.RequireWrites();
        await ResumeReadOnlyAsync(manifest);
        var status = (await Api.SendAsync("GET", $"/guest/groups/{Group}/status", Owner)).Require(200);
        if (status.Json.GetProperty("status").GetString() != "Open" || Guests.Count != 0)
            throw new InvalidOperationException("Continuation requires this run's Open group before any Guest joined.");
        var metadata = (await Api.SendAsync("GET", $"/reservations/{Reservation}/invite-token", Owner)).Require(200);
        Location = metadata.Json.GetProperty("invite").GetProperty("LocationId").GetString()!;
        OwnerSession = (await Api.SendAsync("GET", $"/reservations/{Reservation}/guest-session-id", Owner)).Require(200).Json.GetProperty("guestSessionId").GetString()!;
        ExpectedCarts[OwnerSession] = new ExpectedCart();
        var dishes = (await Api.SendAsync("GET", $"/dishes?locationId={Location}")).Require(200).Json;
        Dishes.AddRange(dishes.EnumerateArray().Where(d => d.GetProperty("state").GetString() == "Available")
            .Select(d => new TestDish(d.GetProperty("id").GetString()!, d.GetProperty("name").GetString()!, d.GetProperty("price").GetDecimal())).Take(2));
        ReadOnly = false;
        IsContinuation = true;
        Lifecycle.Add(new { action = "continued-own-interrupted-scenario", reservationId = Reservation, newReservations = 0 });
    }

    public async Task<TestGuest> JoinAsync(string label)
    {
        var name = $"{RunId}-{label}";
        var reply = (await Api.SendAsync("POST", "/guest/sessions", body: new { groupReservationId = Group, guestName = name })).Require(200);
        var guest = new TestGuest(reply.Json.GetProperty("SessionId").GetString()!, reply.Json.GetProperty("SessionToken").GetString()!, name);
        SecretRedactor.Add(guest.Token);
        Guests.Add(guest);
        ExpectedCarts[guest.Id] = new ExpectedCart();
        await ReviewReadyAsync(Guests.Count + 1);
        return guest;
    }

    public async Task AddAsync(TestGuest guest, int dish = 0, int quantity = 1)
    {
        (await Api.SendAsync("POST", $"/guest/sessions/{guest.Id}/dishes", guest.Token, new { dishId = Dishes[dish].Id, quantity })).Require(200);
        var cart = ExpectedCarts[guest.Id];
        cart.Quantities[Dishes[dish].Id] = cart.Quantities.GetValueOrDefault(Dishes[dish].Id) + quantity;
    }

    public async Task SubmitGuestAsync(TestGuest guest)
    {
        (await Api.SendAsync("POST", $"/guest/sessions/{guest.Id}/submit", guest.Token)).Require(200);
        ExpectedCarts[guest.Id].Submitted = true;
    }

    public async Task OwnerCartAsync(bool submitted = true)
    {
        (await Api.SendAsync("POST", "/orders", Owner, new { reservationId = Reservation, orderItems = new[] { new { dishId = Dishes[0].Id, quantity = 1 } } })).Require(201);
        if (submitted) (await Api.SendAsync("POST", $"/orders/{Reservation}/submit", Owner)).Require(200);
        ExpectedCarts[OwnerSession].Quantities[Dishes[0].Id] = 1;
        ExpectedCarts[OwnerSession].Submitted = submitted;
    }

    public Task<GroupReply> ReviewReadyAsync(int count) => Api.PollAsync(() => Api.ReviewAsync(Group, Owner),
        r => r.Status == 200 && r.Json.GetProperty("participants").GetArrayLength() == count);

    public async ValueTask DisposeAsync()
    {
        if (Reservation.Length == 0 || ReadOnly) return;
        if (BusinessWriteSafety.IsSuspended)
        {
            Lifecycle.Add(new { action = "cleanup-suspended-no-business-writes", reservationId = Reservation });
            return;
        }
        var disposition = FinalSubmitted ? CleanupDisposition.RetainSubmitted : CleanupDisposition.DeleteOwnGuests;
        if (Group.Length > 0)
        {
            var status = (await Api.SendAsync("GET", $"/guest/groups/{Group}/status", Owner)).Require(200);
            FinalSubmitted |= status.Json.GetProperty("status").GetString() == "Submitted";
            disposition = GroupLifecycle.Decide(FinalSubmitted, status.Json.GetProperty("status").GetString()!, SubmissionAttempted);
        }
        // Open groups: remove only this fixture's guests. Minimal HW6 deployment has no reservation
        // cancellation route. Explicit retention mode records every leftover instead of claiming cleanup.
        var failures = new List<string>();
        if (disposition == CleanupDisposition.DeleteOwnGuests && Group.Length > 0)
            foreach (var guest in Guests)
            {
                try
                {
                    var removed = await Api.RemoveAsync(Group, guest.Id, Owner);
                    Lifecycle.Add(new { sessionId = guest.Id, action = "remove-own-guest", status = removed.Status });
                    if (removed.Status is not (200 or 404)) failures.Add($"Own guest cleanup failed: {removed.Status} {removed.Text}");
                }
                catch (Exception error)
                {
                    Lifecycle.Add(new { sessionId = guest.Id, action = "remove-own-guest-failed", error = SecretRedactor.Clean(error.Message) });
                    failures.Add(SecretRedactor.Clean(error.Message));
                }
            }
        if (Environment.GetEnvironmentVariable("AQA_CANCEL_RESERVATIONS") == "true" && disposition == CleanupDisposition.DeleteOwnGuests)
        {
            var cancel = await Api.SendAsync("DELETE", "/reservations", Owner, new { reservationId = Reservation });
            Lifecycle.Add(new { reservationId = Reservation, action = "cancel-own-reservation", status = cancel.Status });
            if (cancel.Status != 200) throw new InvalidOperationException($"Own reservation cleanup failed: {cancel.Status} {cancel.Text}");
        }
        else Lifecycle.Add(new { reservationId = Reservation, groupId = Group, action = "retained-no-cleanup-route", finalSubmitted = FinalSubmitted, cleanupDisposition = disposition.ToString(), reason = "Minimal deployment: no reservation cancellation/hard-delete API, no TTL. Future slot and IDs belong only to this run. Never reused; manual lifecycle review required." });
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }
}
