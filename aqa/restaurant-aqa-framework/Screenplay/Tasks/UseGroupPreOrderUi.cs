using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class UseGroupPreOrderUi(string action) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var f = actor.Recall<GroupFixture>();
        var checks = new GroupChecks();
        actor.Remember(checks);
        var page = actor.LookingAtThePage();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(GroupTestSettings.Require("OWNER_USERNAME"), GroupTestSettings.Require("OWNER_PASSWORD")));
        var ui = new GroupPreOrderPage(page);
        if (action == "journey")
        {
            await ui.OwnerCartAsync(f);
            f.ExpectedCarts[f.OwnerSession].Quantities[f.Dishes[0].Id] = 1;
            f.ExpectedCarts[f.OwnerSession].Submitted = true;
            // A separate anonymous browser context performs a genuine invitation join and cart flow.
            var browser = actor.AbilityTo<IBrowseTheWeb>().Context.Browser!;
            await using var guestContext = await browser.NewContextAsync(new() { BaseURL = Settings.Current.UiBaseUrl });
            guestContext.SetDefaultTimeout(Settings.Current.DefaultTimeoutMs);
            await guestContext.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false });
            var guestPage = await guestContext.NewPageAsync();
            try
            {
                var guestUi = new GuestPreOrderPage(guestPage);
                var joined = await guestPage.RunAndWaitForResponseAsync(() => guestUi.JoinAsync(f.Invite, f.RunId + "-UI"),
                    r => r.Url.EndsWith("/guest/sessions", StringComparison.Ordinal) && r.Status == 200);
                var json = (await joined.JsonAsync())!.Value;
                SecretRedactor.RegisterJson(json);
                var guest = new TestGuest(json.GetProperty("SessionId").GetString()!, json.GetProperty("SessionToken").GetString()!, f.RunId + "-UI");
                SecretRedactor.Add(guest.Token);
                f.Guests.Add(guest);
                await guestUi.AddAndSubmitAsync(f.Dishes[0]);
                f.ExpectedCarts[guest.Id] = new ExpectedCart { Submitted = true };
                f.ExpectedCarts[guest.Id].Quantities[f.Dishes[0].Id] = 2;
                await f.ReviewReadyAsync(2);
            }
            finally
            {
                var path = Path.Combine(ArtifactPaths.Traces, $"guest-{f.RunId}.zip");
                await guestContext.Tracing.StopAsync(new() { Path = path });
                SecretRedactor.CleanTrace(path);
            }
            var removedGuest = await f.JoinAsync("Guest-to-remove");
            await f.AddAsync(removedGuest, 1);
            await f.SubmitGuestAsync(removedGuest);
        }
        var snapshot = (await f.Api.PollAsync(() => f.Api.ReviewAsync(f.Group, f.Owner), r => r.Status == 200 &&
            (action != "journey" || r.Json.GetProperty("participants").EnumerateArray().All(p => p.GetProperty("isSubmitted").GetBoolean())))).Require(200);
        await ui.OpenAsync(f.Reservation);
        await Microsoft.Playwright.Assertions.Expect(ui.Cards).ToHaveCountAsync(snapshot.Json.GetProperty("participants").GetArrayLength());
        foreach (var p in snapshot.Json.GetProperty("participants").EnumerateArray())
        {
            var card = p.GetProperty("type").GetString() == "Owner"
                ? ui.Cards.Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "My Pre-Order", Exact = true }) })
                : ui.Card(p.GetProperty("name").GetString()!);
            await Microsoft.Playwright.Assertions.Expect(card.Locator(".cart-status")).ToHaveTextAsync(p.GetProperty("isSubmitted").GetBoolean() ? "Submitted" : "Still choosing");
            await Microsoft.Playwright.Assertions.Expect(card.Locator(".subtotal")).ToContainTextAsync(GroupPreOrderPage.Money(p.GetProperty("total").GetDecimal()));
            foreach (var item in p.GetProperty("items").EnumerateArray())
            {
                var row = card.GetByRole(AriaRole.Listitem).Filter(new() { HasText = item.GetProperty("dish").GetProperty("name").GetString()! });
                await Microsoft.Playwright.Assertions.Expect(row.GetByLabel("Quantity: " + item.GetProperty("quantity").GetInt32())).ToBeVisibleAsync();
            }
            if (p.GetProperty("items").GetArrayLength() == 0) await Microsoft.Playwright.Assertions.Expect(card.GetByText("No dishes yet", new() { Exact = true })).ToBeVisibleAsync();
        }
        var submittedTotal = snapshot.Json.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("isSubmitted").GetBoolean()).Sum(p => p.GetProperty("total").GetDecimal());
        await Microsoft.Playwright.Assertions.Expect(ui.GrandTotal).ToHaveTextAsync(GroupPreOrderPage.Money(submittedTotal));
        ProbeGroupPreOrder.CheckSnapshot(checks, snapshot.Json, f);
        if (action == "verify") return;
        if (action == "review")
        {
            checks.Check(snapshot.Json.GetProperty("participants").EnumerateArray().Any(p => !p.GetProperty("isSubmitted").GetBoolean() && p.GetProperty("items").GetArrayLength() == 0), "Empty draft participant missing.");
            return;
        }
        if (action is "remove" or "journey")
        {
            var target = action == "journey" ? f.Guests[^1] : f.Guests[0];
            await ui.RemoveAsync(target.Name);
            await Microsoft.Playwright.Assertions.Expect(ui.Card(target.Name)).ToHaveCountAsync(0);
            snapshot = (await f.Api.PollAsync(() => f.Api.ReviewAsync(f.Group, f.Owner), r => r.Status == 200 && !r.Json.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("id").GetString() == target.Id))).Require(200);
            submittedTotal = snapshot.Json.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("isSubmitted").GetBoolean()).Sum(p => p.GetProperty("total").GetDecimal());
            await Microsoft.Playwright.Assertions.Expect(ui.GrandTotal).ToHaveTextAsync(GroupPreOrderPage.Money(submittedTotal));
            if (action == "remove") return;
        }
        await ui.SubmitAsync();
        // Remember the write before waiting for the UI so cleanup can handle failed transitions safely.
        var status = await f.Api.SendAsync("GET", $"/guest/groups/{f.Group}/status", f.Owner);
        f.FinalSubmitted = status.Status == 200 && status.Json.GetProperty("status").GetString() == "Submitted";
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".read-only")).ToContainTextAsync("Sent to Restaurant");
        await page.ReloadAsync();
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".read-only")).ToContainTextAsync("read-only");
        await Microsoft.Playwright.Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true })).ToHaveCountAsync(0);
        await Microsoft.Playwright.Assertions.Expect(page.Locator("app-participant-card button")).ToHaveCountAsync(0);
        await ProbeGroupPreOrder.CheckFinalAsync(checks, f, snapshot.Json);
    }
}
