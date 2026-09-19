using CyQuotePlugin.Services;
using System.Text;

namespace CyQuotePlugin.Tests;

public class QuoteCatalogTests
{
    private const string Jsonc = """
        // catalog
        {
          "A": [
            { "value": "one", "author": "author", },
            { "value": "  " }
          ],
          "B": [ { "value": "two", "from": "book" } ],
          "Empty": [],
        }
        """;

    [Fact]
    public void Parse_AcceptsJsoncAndDropsInvalidEntries()
    {
        var snapshot = QuoteCatalog.Parse(Jsonc);

        Assert.Equal(["A", "B"], snapshot.Categories);
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("book", snapshot.Pick(["B"], new Random(1))!.Source);
    }

    [Fact]
    public void Pick_UsesOnlyRequestedCategories()
    {
        var snapshot = QuoteCatalog.Parse(Jsonc);

        Assert.Equal("one", snapshot.Pick(["A"], new Random(1))!.Value);
        Assert.Equal("two", snapshot.Pick(["B"], new Random(1))!.Value);
    }

    [Fact]
    public void Pick_FallsBackToAllWhenNoRequestedCategoryExists()
    {
        var snapshot = QuoteCatalog.Parse(Jsonc);

        var values = Enumerable.Range(0, 20)
            .Select(_ => snapshot.Pick(["Missing"], Random.Shared)!.Value)
            .ToHashSet();

        Assert.Subset(new HashSet<string> { "one", "two" }, values);
        Assert.NotEmpty(values);
    }

    [Fact]
    public void Publish_SubscriberFailureDoesNotEscape()
    {
        var catalog = new QuoteCatalog();
        catalog.Changed += (_, _) => throw new InvalidOperationException("subscriber failed");
        var snapshot = QuoteCatalog.Parse(Jsonc);

        var exception = Record.Exception(() => catalog.Publish(snapshot));

        Assert.Null(exception);
        Assert.Same(snapshot, catalog.Current);
    }

    [Fact]
    public void ParseUtf8_RejectsInvalidEncoding()
    {
        var corrupt = Encoding.UTF8.GetBytes(Jsonc);
        corrupt[20] = 0xff;

        Assert.Throws<DecoderFallbackException>(() => QuoteCatalog.ParseUtf8(corrupt));
    }
}
