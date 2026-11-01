using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;

namespace Restaurant_Aqa_Framework.Api;

public sealed record GroupReply(int Status, JsonElement Json)
{
    public string Text => SecretRedactor.Clean(Json.ToString());
    public GroupReply Require(int status)
    {
        if (Status != status)
            throw new InvalidOperationException($"API setup expected {status}, received {Status}: {Text}");
        return this;
    }
}

/// <summary>No mutation retries. Only bounded, observable read polling for DynamoDB GSI propagation.</summary>
public sealed class GroupPreOrderApi(IAPIRequestContext context)
{
    private static int reservationAttempts;
    public ConcurrentQueue<object> Diagnostics { get; } = new();

    public async Task<GroupReply> SendAsync(string method, string path, string? token = null, object? body = null)
    {
        BusinessWriteSafety.CheckRequest(method, path);
        if (method == "POST" && path == "/reservations")
        {
            var limit = BusinessWriteSafety.ParseReservationLimit(Environment.GetEnvironmentVariable("AQA_MAX_RESERVATIONS"));
            if (limit < 1 || Interlocked.Increment(ref reservationAttempts) > limit)
                throw new InvalidOperationException("Reservation creation budget exhausted. No additional request was sent.");
        }
        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/json", ["Origin"] = Settings.Current.UiBaseUrl };
        if (token is not null) headers["Authorization"] = $"Bearer {token}";
        var response = await context.FetchAsync(Settings.Current.ApiBaseUrl.TrimEnd('/') + path,
            new APIRequestContextOptions { Method = method, Headers = headers, DataString = body is null ? null : JsonSerializer.Serialize(body) });
        var text = await response.TextAsync();
        JsonElement json;
        try { json = JsonSerializer.Deserialize<JsonElement>(text); }
        catch (JsonException) { json = JsonSerializer.SerializeToElement(text); }
        SecretRedactor.RegisterJson(json);
        Diagnostics.Enqueue(new { method, path = SecretRedactor.Clean(path), status = response.Status, at = DateTimeOffset.UtcNow });
        var result = new GroupReply(response.Status, json);
        await response.DisposeAsync();
        return result;
    }

    public async Task<string> LoginAsync(string role)
    {
        var prefix = role.ToUpperInvariant();
        var email = GroupTestSettings.Require($"{prefix}_USERNAME");
        var password = GroupTestSettings.Require($"{prefix}_PASSWORD");
        SecretRedactor.Add(email, password);
        var reply = (await SendAsync("POST", "/signin", body: new { email, password })).Require(200);
        var token = reply.Json.GetProperty("idToken").GetString()!;
        SecretRedactor.Add(token);
        foreach (var key in new[] { "accessToken", "refreshToken" })
            if (reply.Json.TryGetProperty(key, out var value)) SecretRedactor.Add(value.GetString());
        var profile = (await SendAsync("GET", "/me", token)).Require(200);
        var actualRole = profile.Json.GetProperty("role").GetString();
        if (role is "owner" or "other" && actualRole != "Customer")
            throw new InvalidOperationException($"{role} fixture requires Customer role; actual {actualRole}.");
        return token;
    }

    public Task<GroupReply> ReviewAsync(string group, string? token) => SendAsync("GET", $"/guest/groups/{group}/sessions", token);
    public Task<GroupReply> RemoveAsync(string group, string session, string? token) => SendAsync("DELETE", $"/guest/groups/{group}/sessions/{session}", token);
    public Task<GroupReply> SubmitAsync(string group, string reservation, string? token) => SendAsync("POST", $"/group-reservations/{group}/submit", token, new { reservationId = reservation });

    public async Task<GroupReply> PollAsync(Func<Task<GroupReply>> read, Func<GroupReply, bool> ready, bool allowNotFound = false, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        GroupReply result;
        do
        {
            result = await read();
            Diagnostics.Enqueue(new { action = "consistency-read", status = result.Status });
            if (ready(result)) return result;
            if (result.Status >= 400 && !(allowNotFound && result.Status == 404)) return result;
            // A bounded read consistency poll, never a retry of a failed business action.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(500, (deadline - DateTime.UtcNow).TotalMilliseconds))));
        } while (DateTime.UtcNow < deadline);
        throw new TimeoutException($"Required API state was not observed before the consistency deadline; last status={result.Status}. No mutation was retried.");
    }
}
