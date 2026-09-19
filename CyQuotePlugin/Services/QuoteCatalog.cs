using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using CyQuotePlugin.Models;

namespace CyQuotePlugin.Services;

public sealed class QuoteCatalog
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private QuoteSnapshot _current = QuoteSnapshot.Empty;

    public QuoteSnapshot Current => Volatile.Read(ref _current);

    public event EventHandler? Changed;

    public static QuoteSnapshot Parse(string jsonc)
    {
        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        var source = JsonSerializer.Deserialize<Dictionary<string, List<QuoteDto>>>(jsonc, options)
                     ?? throw new JsonException("语录数据为空。");
        var categories = source
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .Select(pair => new
            {
                Name = pair.Key.Trim(),
                Quotes = pair.Value
                    .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                    .Select(item => new Quote(
                        item.Value.Trim(),
                        item.Author?.Trim() ?? "",
                        item.Source?.Trim() ?? ""))
                    .ToArray()
            })
            .Where(pair => pair.Quotes.Length > 0)
            .OrderBy(pair => pair.Name, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Name, pair => pair.Quotes, StringComparer.Ordinal);

        return new QuoteSnapshot(categories);
    }

    public static QuoteSnapshot ParseUtf8(ReadOnlySpan<byte> bytes) =>
        Parse(StrictUtf8.GetString(bytes));

    public void Publish(QuoteSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        if (Changed is null) return;
        foreach (EventHandler handler in Changed.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch
            {
                // A stale UI subscriber must not roll back an already published snapshot.
            }
        }
    }

    public Quote? Pick(IReadOnlyCollection<string> categories, Random? random = null) =>
        Current.Pick(categories, random ?? Random.Shared);

    private sealed class QuoteDto
    {
        public string Value { get; init; } = "";
        public string? Author { get; init; }

        [JsonPropertyName("from")]
        public string? Source { get; init; }
    }
}

public sealed class QuoteSnapshot
{
    private readonly IReadOnlyDictionary<string, Quote[]> _quotes;
    private readonly Quote[] _all;

    internal QuoteSnapshot(IReadOnlyDictionary<string, Quote[]> quotes)
    {
        _quotes = quotes;
        Categories = quotes.Keys.ToArray();
        _all = quotes.Values.SelectMany(items => items).ToArray();
    }

    public static QuoteSnapshot Empty { get; } =
        new(new Dictionary<string, Quote[]>(StringComparer.Ordinal));

    public IReadOnlyList<string> Categories { get; }
    public int Count => _all.Length;

    public Quote? Pick(IReadOnlyCollection<string> categories, Random random)
    {
        var selected = categories
            .Where(_quotes.ContainsKey)
            .SelectMany(category => _quotes[category])
            .ToArray();
        var pool = selected.Length > 0 ? selected : _all;
        return pool.Length == 0 ? null : pool[random.Next(pool.Length)];
    }
}
