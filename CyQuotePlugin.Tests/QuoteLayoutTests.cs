using CyQuotePlugin.Services;

namespace CyQuotePlugin.Tests;

public class QuoteLayoutTests
{
    private const string Attribution = "流萤 · 火萤";

    /// <summary>
    /// 模拟排版引擎：全角字宽 = 字号，半角 = 字号的一半，按 maxWidth 折行；
    /// 排不下时返回 maxLines + 1，与 QuoteComponent.CountLines 的约定一致。
    /// </summary>
    private static Func<string, double, int> Simulate(double maxWidth, int maxLines) => (text, fontSize) =>
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var width = text.Sum(ch => IsWide(ch) ? fontSize : fontSize / 2.0);
        var lines = (int)Math.Ceiling(width / maxWidth - 1e-9);
        if (lines < 1) lines = 1;
        return lines > maxLines ? maxLines + 1 : lines;
    };

    private static bool IsWide(char ch) => ch >= 0x2E80;

    [Fact]
    public void ShortQuote_KeepsBaseFontSizeAndStaysOnOneLine()
    {
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 2);

        var result = QuoteLayout.Arrange("星光不负赶路人", Attribution, options, Simulate(1200, 2));

        Assert.Equal("星光不负赶路人 · 流萤 · 火萤", result.Text);
        Assert.Equal(20, result.FontSize, 3);
        Assert.False(result.Truncated);
        Assert.Equal(1, result.LineCount);
    }

    [Fact]
    public void TwoLineQuote_ShrinksFontSizeToUsableHeightWithoutTruncating()
    {
        // 100 个全角字：20px 时约 2110px 宽，单行放不下；降到可用高度允许的字号后两行能装下
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 2);
        var body = new string('字', 100);

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1200, 2));

        Assert.Equal(45 / (2 * QuoteLayout.LineHeightRatio), result.FontSize, 3);
        Assert.True(result.FontSize < 20);
        Assert.False(result.Truncated);
        Assert.Equal($"{body} · {Attribution}", result.Text);
        Assert.True(result.LineCount <= 2);
    }

    [Fact]
    public void MediumQuote_PicksLargestFontThatStillFits_NotTheBudgetFloor()
    {
        // 上限 1872、原字号 40：预算字号只有 15.5px，但 27px 出头就能单行装下——应当取大的那个
        var options = new QuoteLayout.Options(40, 1872, 45, 14, 2);
        var body = "有两种东西，我们愈是时常反复地思索，它们就愈是给人的心灵灌注了时时翻新、有加无已的赞叹和敬畏：头顶的星空和心中的道德律令。";

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1872, 2));

        Assert.False(result.Truncated);
        Assert.Equal(1, result.LineCount);
        Assert.True(
            result.FontSize > 45 / (2 * QuoteLayout.LineHeightRatio),
            $"应当优先放大字号，实际 {result.FontSize:F1}px");
    }

    [Fact]
    public void LongQuote_TruncatesBeforeAttribution()
    {
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 2);
        var body = new string('字', 400);

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1200, 2));

        Assert.True(result.Truncated);
        Assert.EndsWith(Attribution, result.Text);
        Assert.Contains('…', result.Text);

        var ellipsis = result.Text.IndexOf('…');
        var attribution = result.Text.IndexOf(Attribution, StringComparison.Ordinal);
        Assert.True(ellipsis >= 0);
        Assert.True(attribution > ellipsis, "省略号必须落在署名之前");
        Assert.True(ellipsis < body.Length, "截断的是正文，不是署名");
    }

    [Fact]
    public void Truncation_SnapsBackToSentencePunctuation()
    {
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 2);
        var body = string.Concat(Enumerable.Repeat("生命短暂如流萤，却仍要在夜里发出比星星更亮的光。", 10));

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1200, 2));

        Assert.True(result.Truncated);
        var ellipsis = result.Text.IndexOf('…');
        Assert.True(ellipsis > 0);
        Assert.Contains(result.Text[ellipsis - 1], QuoteLayout.SentencePunctuations);
        Assert.EndsWith(Attribution, result.Text);
    }

    [Fact]
    public void AttributionSurvivesEvenWhenFontFloorBlocksFitting()
    {
        // 可用高度不限、字号下限 = 原字号：只能靠截断解决，署名依然不能被吃掉
        var options = new QuoteLayout.Options(20, 1200, 0, 20, 2);
        var body = new string('字', 400);

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1200, 2));

        Assert.True(result.Truncated);
        Assert.Equal(20, result.FontSize, 3);
        Assert.EndsWith(Attribution, result.Text);
    }

    [Fact]
    public void EmptyBody_ReturnsAttributionOnly()
    {
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 2);

        var result = QuoteLayout.Arrange("", Attribution, options, Simulate(1200, 2));

        Assert.Equal(Attribution, result.Text);
        Assert.False(result.Truncated);
        Assert.Equal(1, result.LineCount);
    }

    [Fact]
    public void AdaptiveDisabled_KeepsOriginalFontSizeAndText()
    {
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 2, Adaptive: false);
        var body = new string('字', 400);

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1200, 2));

        Assert.Equal(20, result.FontSize, 3);
        Assert.False(result.Truncated);
        Assert.Equal($"{body} · {Attribution}", result.Text);
        Assert.True(result.LineCount > 1);
    }

    [Fact]
    public void SingleMaxLine_TruncatesToFirstLineAndKeepsAttribution()
    {
        var options = new QuoteLayout.Options(20, 1200, 45, 14, 1);
        var body = new string('字', 400);

        var result = QuoteLayout.Arrange(body, Attribution, options, Simulate(1200, 1));

        Assert.True(result.Truncated);
        Assert.Equal(1, result.LineCount);
        Assert.EndsWith(Attribution, result.Text);
    }

    [Theory]
    [InlineData(0, 0)]   // keep = 0 原样返回
    [InlineData(3, 3)]   // 断点正好落在句读之后
    [InlineData(6, 3)]   // 断点切在句中，回退到「。」之后
    [InlineData(10, 8)]  // keep 超出长度时按末尾句读收敛
    public void SnapToPunctuation_MovesBreakpointAfterNearestMark(int keep, int expected)
    {
        var body = "短句。再来一句。";

        Assert.Equal(expected, QuoteLayout.SnapToPunctuation(body, keep));
    }
}
