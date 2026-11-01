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
        var dish = Page.Locator("app-dish-card").Filter(new() { HasText = fixture.Dishes[0].Name });
        await dish.GetByRole(AriaRole.Button, new() { Name = "Pre-Order", Exact = true }).ClickAsync();
        // Wait for the real order, then scope the cart action by its generated order ID.
        var order = await fixture.Api.PollAsync(() => fixture.Api.SendAsync("GET", $"/orders/{fixture.Reservation}", fixture.Owner), r => r.Status == 200);
        order.Require(200);
        var orderId = order.Json.GetProperty("orderId").GetString()!;
        await Page.GetByRole(AriaRole.Button, new() { Name = "cart icon Cart", Exact = true }).ClickAsync();
        var target = Page.Locator("app-not-submitted-order").Filter(new() { HasText = orderId });
        await target.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-order", Exact = true }).ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(target).ToHaveCountAsync(0);
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
