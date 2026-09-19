namespace CyQuotePlugin.Models;

public sealed record Quote(string Value, string Author = "", string Source = "")
{
    public static string FormatSingleLine(
        string quote,
        string author,
        string source,
        bool showAuthor,
        bool showSource) => string.Join(" · ", new[]
    {
        quote,
        showAuthor ? author : "",
        showSource ? source : ""
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
