using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Skia;
using CyQuotePlugin.Services;

namespace CyQuotePlugin.Tests;

/// <summary>
/// 用真实 Avalonia 排版引擎验证行数判定。
/// 这里守的是一个隐蔽 bug：MaxLines 截断时 HasCollapsed 不一定为 true，
/// 只看它会把"署名尾巴被砍"误判成"两行排下了"。
/// </summary>
public class QuoteTextLayoutTests
{
    private const double FontSize = 20;
    private const double MaxWidth = 1200;
    private const int MaxLines = 2;

    private static readonly object Gate = new();
    private static bool _ready;

    private static void EnsureEngine()
    {
        lock (Gate)
        {
            if (_ready) return;
            AppBuilder.Configure<TestApplication>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
            _ready = true;
        }
    }

    private sealed class TestApplication : Application
    {
    }

    [Fact]
    public void ShortText_FitsOnOneLine()
    {
        EnsureEngine();

        Assert.Equal(1, QuoteTextLayout.CountLines("星光不负赶路人", FontSize, MaxWidth, MaxLines));
    }

    [Fact]
    public void EmptyText_ReturnsZero()
    {
        EnsureEngine();

        Assert.Equal(0, QuoteTextLayout.CountLines("", FontSize, MaxWidth, MaxLines));
    }

    [Fact]
    public void InfiniteWidth_AlwaysSingleLine()
    {
        EnsureEngine();

        Assert.Equal(1, QuoteTextLayout.CountLines(new string('字', 400), FontSize, double.PositiveInfinity, MaxLines));
    }

    [Fact]
    public void NonPositiveWidth_AlwaysSingleLine()
    {
        EnsureEngine();

        Assert.Equal(1, QuoteTextLayout.CountLines(new string('字', 400), FontSize, 0, MaxLines));
    }

    [Fact]
    public void TextFarBeyondTwoLines_IsReportedAsNotFitting()
    {
        EnsureEngine();

        var lines = QuoteTextLayout.CountLines(new string('字', 400), FontSize, MaxWidth, MaxLines);

        Assert.True(lines > MaxLines, $"期望报放不下，实际返回 {lines}");
    }

    [Fact]
    public void AttributionTailLostToMaxLines_IsAlsoReportedAsNotFitting()
    {
        EnsureEngine();
        var text = new string('字', 400) + " · 康德 · 《实践理性批判》";

        var lines = QuoteTextLayout.CountLines(text, FontSize, MaxWidth, MaxLines);

        Assert.True(lines > MaxLines, $"署名尾巴被 MaxLines 砍掉时必须报放不下，实际返回 {lines}");
    }

    /// <summary>
    /// 回归测试：排版引擎只吐两行，文本末尾根本没排进来——这就是署名丢字的根源。
    /// 注意 HasCollapsed 在不同内容上并不总是 false（取决于最后一行自身是否超宽），
    /// 所以判定必须看"是否排到了文本末尾"，不能只看 HasCollapsed。
    /// </summary>
    [Fact]
    public void MaxLinesTruncation_DropsTailAndIsCaughtByCoverageCheck()
    {
        EnsureEngine();
        var text = new string('字', 400) + " · 康德 · 《实践理性批判》";

        var layout = new TextLayout(
            text,
            new Typeface(FontManager.Current.DefaultFontFamily),
            FontSize,
            Brushes.Transparent,
            TextAlignment.Center,
            TextWrapping.Wrap,
            TextTrimming.CharacterEllipsis,
            null,
            FlowDirection.LeftToRight,
            MaxWidth,
            double.PositiveInfinity,
            0,
            0,
            MaxLines,
            null);

        Assert.Equal(MaxLines, layout.TextLines.Count);

        var last = layout.TextLines[^1];
        Assert.True(
            last.FirstTextSourceIndex + last.Length < text.Length,
            "期望文本没排完（末尾被丢弃），否则这条测试的前提不成立");

        Assert.True(
            QuoteTextLayout.CountLines(text, FontSize, MaxWidth, MaxLines) > MaxLines,
            "文本没排完时必须报放不下，否则会在署名前下不了刀");
    }

    /// <summary>端到端：QuoteLayout + 真实排版引擎，长语录必须在署名前断刀且结果真的排得下。</summary>
    [Fact]
    public void ArrangeWithRealEngine_TruncatesBeforeAttributionAndResultFits()
    {
        EnsureEngine();
        const string attribution = "康德 · 《实践理性批判》";
        var body = new string('字', 400);
        var options = new QuoteLayout.Options(BaseFontSize: FontSize, MaxWidth: MaxWidth,
            UsableHeight: 45, MinFontSize: 14, MaxLines: MaxLines);

        var result = QuoteLayout.Arrange(body, attribution, options,
            (text, fontSize) => QuoteTextLayout.CountLines(text, fontSize, MaxWidth, MaxLines));

        Assert.True(result.Truncated);
        Assert.EndsWith(attribution, result.Text);
        Assert.Contains('…', result.Text);
        Assert.True(
            result.Text.IndexOf('…') < result.Text.IndexOf(attribution, StringComparison.Ordinal),
            "省略号必须落在署名之前");
        Assert.True(
            QuoteTextLayout.CountLines(result.Text, result.FontSize, MaxWidth, MaxLines) <= MaxLines,
            "算出来的文本必须真的排得下");
    }

    /// <summary>端到端：短语录走单行，原字号原样输出。</summary>
    [Fact]
    public void ArrangeWithRealEngine_KeepsShortQuoteIntact()
    {
        EnsureEngine();
        var options = new QuoteLayout.Options(BaseFontSize: FontSize, MaxWidth: MaxWidth,
            UsableHeight: 45, MinFontSize: 14, MaxLines: MaxLines);

        var result = QuoteLayout.Arrange("星光不负赶路人", "流萤 · 火萤", options,
            (text, fontSize) => QuoteTextLayout.CountLines(text, fontSize, MaxWidth, MaxLines));

        Assert.False(result.Truncated);
        Assert.Equal(FontSize, result.FontSize, 3);
        Assert.Equal("星光不负赶路人 · 流萤 · 火萤", result.Text);
    }
}
