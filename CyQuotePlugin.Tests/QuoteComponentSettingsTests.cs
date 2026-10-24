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

    [Fact]
    public void AdaptiveLayout_DefaultsToTrue()
    {
        var settings = new QuoteComponentSettings();

        Assert.True(settings.AdaptiveLayout);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(9, 4)]
    public void MaxLines_ClampsToOneThroughFour(int input, int expected)
    {
        var settings = new QuoteComponentSettings { MaxLines = input };

        Assert.Equal(expected, settings.MaxLines);
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(6, 6)]
    [InlineData(14, 14)]
    [InlineData(72, 72)]
    [InlineData(200, 72)]
    public void MinFontSize_ClampsToSixThroughSeventyTwo(double input, double expected)
    {
        var settings = new QuoteComponentSettings { MinFontSize = input };

        Assert.Equal(expected, settings.MinFontSize, 3);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    [InlineData(500, 500)]
    [InlineData(9999, 500)]
    public void MaxBlockHeight_ClampsToZeroThroughFiveHundred(double input, double expected)
    {
        var settings = new QuoteComponentSettings { MaxBlockHeight = input };

        Assert.Equal(expected, settings.MaxBlockHeight, 3);
    }
}
