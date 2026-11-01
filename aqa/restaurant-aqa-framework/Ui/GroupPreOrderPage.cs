using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GroupPreOrderPage(IPage page)
{
    public IPage Page { get; } = page;
    public ILocator Cards => Page.Locator("app-participant-card");
    public ILocator Card(string name) => Cards.Filter(new() { HasText = name });
    public ILocator GrandTotal => Page.Locator(".grand-total strong");
    public Task OpenAsync(string reservation) => Page.GotoAsync($"/reservations/{reservation}/group-order");
    public async Task RemoveAsync(string name)
    {
        await Card(name).GetByRole(AriaRole.Button, new() { Name = "Remove " + name, Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Remove Guest", Exact = true }).ClickAsync();
    }
    public async Task SubmitAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
    }
    public static string Money(decimal amount) => amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));

    public async Task OwnerCartAsync(GroupFixture fixture)
    {
        await Page.GotoAsync($"/menu?preOrder=true&reservationId={fixture.Reservation}&reservationLabel={fixture.RunId}");
        var existing = await fixture.Api.SendAsync("GET", $"/orders/{fixture.Reservation}", fixture.Owner);
        if (existing.Status == 404)
        {
            var dish = Page.Locator("app-dish-card").Filter(new() { HasText = fixture.Dishes[0].Name });
            await Page.RunAndWaitForResponseAsync(() => dish.GetByRole(AriaRole.Button, new() { Name = "Pre-Order", Exact = true }).ClickAsync(),
                r => r.Url.EndsWith("/orders", StringComparison.Ordinal) && r.Request.Method == "POST" && r.Status == 201);
        }
        else existing.Require(200); // only the same scenario's interrupted cart may already exist
        // Wait for the real order, then scope the cart action by its generated order ID.
        var order = await fixture.Api.PollAsync(() => fixture.Api.SendAsync("GET", $"/orders/{fixture.Reservation}", fixture.Owner), r => r.Status == 200, allowNotFound: true);
        order.Require(200);
        var items = order.Json.GetProperty("orderItems").EnumerateArray().ToArray();
        if (items.Length != 1 || items[0].GetProperty("dish").GetProperty("id").GetString() != fixture.Dishes[0].Id || items[0].GetProperty("quantity").GetInt32() != 1)
            throw new InvalidOperationException("Own UI cart differs from the one dish added; refusing to modify an unexpected cart.");
        var review = (await fixture.Api.ReviewAsync(fixture.Group, fixture.Owner)).Require(200);
        if (review.Json.GetProperty("participants").EnumerateArray().Single(p => p.GetProperty("type").GetString() == "Owner").GetProperty("isSubmitted").GetBoolean())
            return; // the same interrupted scenario already submitted this exact cart in its previous UI phase
        var orderId = order.Json.GetProperty("orderId").GetString()!;
        await Page.GetByRole(AriaRole.Button, new() { Name = "cart icon", Exact = true }).ClickAsync();
        var target = Page.Locator("app-not-submitted-order").Filter(new() { HasText = orderId });
        await target.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-order", Exact = true }).ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Pre-order Submitted!", Exact = true })).ToBeVisibleAsync();
        var confirmed = await fixture.Api.PollAsync(() => fixture.Api.ReviewAsync(fixture.Group, fixture.Owner), r => r.Status == 200 &&
            r.Json.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("type").GetString() == "Owner" && p.GetProperty("isSubmitted").GetBoolean()));
        if (!confirmed.Json.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("type").GetString() == "Owner" && p.GetProperty("isSubmitted").GetBoolean()))
            throw new InvalidOperationException("Owner cart did not become submitted after the UI confirmation.");
    }
}

public sealed class GuestPreOrderPage(IPage page)
{
    public async Task JoinAsync(string invite, string name)
    {
        await page.GotoAsync("/join/" + Uri.EscapeDataString(invite));
        await page.GetByLabel("Enter Your Name", new() { Exact = true }).FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Browse Menu", Exact = true }).ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(page.GetByLabel("Enter Your Name", new() { Exact = true })).ToHaveCountAsync(0);
    }

    public async Task AddAndSubmitAsync(TestDish dish)
    {
        var card = page.Locator("app-dish-card").Filter(new() { HasText = dish.Name });
        await card.GetByRole(AriaRole.Button, new() { Name = "Pre-Order", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Open cart", Exact = true }).ClickAsync();
        var cart = page.GetByRole(AriaRole.Dialog, new() { Name = "Cart", Exact = true });
        await cart.GetByRole(AriaRole.Button, new() { Name = "Increase quantity", Exact = true }).ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(cart.Locator(".qty-value")).ToHaveTextAsync("2");
        await cart.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true }).ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Pre-Order Submitted!", Exact = true })).ToBeVisibleAsync();
    }
}
