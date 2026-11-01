using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Restaurant_Aqa_Framework.Core;

public static partial class SecretRedactor
{
    private static readonly ConcurrentDictionary<string, byte> Secrets = new(StringComparer.Ordinal);
    public static void Add(params string?[] values)
    {
        foreach (var value in values.Where(v => !string.IsNullOrEmpty(v)))
        {
            Secrets.TryAdd(value!, 0);
            Secrets.TryAdd(Uri.EscapeDataString(value!), 0);
            Secrets.TryAdd(JsonSerializer.Serialize(value)[1..^1], 0);
        }
    }

    public static string Clean(string text)
    {
        foreach (var secret in Secrets.Keys.OrderByDescending(s => s.Length)) text = text.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        return JwtPattern().Replace(text, "[REDACTED-JWT]");
    }

    public static void RegisterJson(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Object)
            foreach (var property in json.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    (property.Name.Contains("token", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("password", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("email", StringComparison.OrdinalIgnoreCase)))
                    Add(property.Value.GetString());
                else RegisterJson(property.Value);
            }
        else if (json.ValueKind == JsonValueKind.Array)
            foreach (var item in json.EnumerateArray()) RegisterJson(item);
    }

    public static void CleanTrace(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        foreach (var entry in zip.Entries.ToArray())
        {
            // Screenshots contain only the app UI. Network payload resources may have opaque names.
            if (entry.FullName.EndsWith(".jpeg", StringComparison.Ordinal) || entry.FullName.EndsWith(".png", StringComparison.Ordinal)) continue;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            var cleaned = Clean(content);
            if (cleaned == content) continue;
            var name = entry.FullName;
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(cleaned);
        }
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+")]
    private static partial Regex JwtPattern();
}
