using System.Text.Json;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public static class GroupOrderAssertions
{
    private static Dictionary<string, int> SubmittedQuantities(JsonElement review) => review.GetProperty("participants").EnumerateArray()
        .Where(p => p.GetProperty("isSubmitted").GetBoolean()).SelectMany(p => p.GetProperty("items").EnumerateArray())
        .GroupBy(i => i.GetProperty("dish").GetProperty("id").GetString()!, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.Sum(i => i.GetProperty("quantity").GetInt32()), StringComparer.Ordinal);

    public static void CheckSubmittedResponse(GroupChecks checks, JsonElement response, JsonElement review) =>
        CheckQuantities(checks, SubmittedQuantities(review), response.EnumerateArray().Select(i => (i.GetProperty("dishId").GetString()!, i.GetProperty("quantity").GetInt32())).ToArray());

    public static void CheckOrder(GroupChecks checks, JsonElement order, JsonElement review)
    {
        var items = order.GetProperty("orderItems").EnumerateArray().ToArray();
        CheckQuantities(checks, SubmittedQuantities(review), items.Select(i => (i.GetProperty("dish").GetProperty("id").GetString()!, i.GetProperty("quantity").GetInt32())).ToArray());
        var expectedDishes = review.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("isSubmitted").GetBoolean())
            .SelectMany(p => p.GetProperty("items").EnumerateArray()).GroupBy(i => i.GetProperty("dish").GetProperty("id").GetString()!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().GetProperty("dish"), StringComparer.Ordinal);
        checks.Check(items.All(i => expectedDishes.TryGetValue(i.GetProperty("dish").GetProperty("id").GetString()!, out var dish) &&
            dish.GetProperty("price").GetDecimal() == i.GetProperty("dish").GetProperty("price").GetDecimal() &&
            dish.GetProperty("name").GetString() == i.GetProperty("dish").GetProperty("name").GetString()), "Final dish name or unit price differs from the submitted cart.");
        var total = items.Sum(i => i.GetProperty("dish").GetProperty("price").GetDecimal() * i.GetProperty("quantity").GetInt32());
        var expectedTotal = review.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("isSubmitted").GetBoolean()).Sum(p => p.GetProperty("total").GetDecimal());
        checks.Check(total == expectedTotal, "Final order total differs from submitted participant subtotals.");
    }

    private static void CheckQuantities(GroupChecks checks, Dictionary<string, int> expected, (string Id, int Quantity)[] actual)
    {
        checks.Check(expected.Count > 0, "A successful final submit must contain submitted dishes.");
        checks.Check(actual.Length == expected.Count, "Consolidated result has missing, duplicate or draft dishes.");
        checks.Check(actual.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() == actual.Length, "Duplicate dish IDs were not consolidated.");
        checks.Check(expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actual.Select(i => i.Id)), "Consolidated dish IDs differ from submitted carts.");
        checks.Check(actual.All(i => expected.TryGetValue(i.Id, out var q) && q == i.Quantity), "Consolidated quantities differ from submitted carts.");
    }

    public static void CheckRemoval(GroupChecks checks, JsonElement before, JsonElement after, string removedId)
    {
        var participants = before.GetProperty("participants").EnumerateArray().ToArray();
        var removed = participants.Single(p => p.GetProperty("id").GetString() == removedId);
        var expected = JsonSerializer.SerializeToElement(new
        {
            participants = participants.Where(p => p.GetProperty("id").GetString() != removedId).ToArray(),
            total = before.GetProperty("total").GetDecimal() - removed.GetProperty("total").GetDecimal(),
        });
        checks.Check(CanonicalReview(expected) == CanonicalReview(after), "Remove changed surviving carts or failed to subtract exactly the removed subtotal.");
    }

    public static string CanonicalReview(JsonElement review) => JsonSerializer.Serialize(new
    {
        total = review.GetProperty("total").GetDecimal(),
        participants = review.GetProperty("participants").EnumerateArray().OrderBy(p => p.GetProperty("id").GetString(), StringComparer.Ordinal)
            .Select(p => new { id = p.GetProperty("id").GetString(), type = p.GetProperty("type").GetString(), name = p.GetProperty("name").GetString(), submitted = p.GetProperty("isSubmitted").GetBoolean(), total = p.GetProperty("total").GetDecimal(), items = CanonicalItems(p.GetProperty("items")) }).ToArray(),
    });

    public static string CanonicalItems(JsonElement items) => JsonSerializer.Serialize(items.EnumerateArray()
        .OrderBy(i => i.GetProperty("dish").GetProperty("id").GetString(), StringComparer.Ordinal)
        .Select(i => new { id = i.GetProperty("dish").GetProperty("id").GetString(), name = i.GetProperty("dish").GetProperty("name").GetString(), price = i.GetProperty("dish").GetProperty("price").GetDecimal(), quantity = i.GetProperty("quantity").GetInt32() }).ToArray());
}
