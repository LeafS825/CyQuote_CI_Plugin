using CyQuotePlugin.Models;

namespace CyQuotePlugin.Tests;

public class QuoteComponentSettingsTests
{
    [Fact]
    public void SingleLine_DefaultsToFalseAndRaisesPropertyChanged()
    {
        var settings = new QuoteComponentSettings();
        string? changedProperty = null;
        settings.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        Assert.False(settings.SingleLine);

        settings.SingleLine = true;

        Assert.True(settings.SingleLine);
        Assert.Equal(nameof(QuoteComponentSettings.SingleLine), changedProperty);
    }

    [Theory]
    [InlineData("正文", "作者", "出处", true, true, "正文 · 作者 · 出处")]
    [InlineData("正文", "", "出处", true, true, "正文 · 出处")]
    [InlineData("正文", "作者", "出处", false, false, "正文")]
    public void FormatSingleLine_JoinsOnlyVisibleNonEmptySegments(
        string quote,
        string author,
        string source,
        bool showAuthor,
        bool showSource,
        string expected)
    {
        var result = Quote.FormatSingleLine(quote, author, source, showAuthor, showSource);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    [InlineData(86400, 86400)]
    [InlineData(100000, 86400)]
    public void ChangeIntervalSeconds_NormalizesValue(int input, int expected)
    {
        var settings = new QuoteComponentSettings { ChangeIntervalSeconds = input };

        Assert.Equal(expected, settings.ChangeIntervalSeconds);
    }
}
