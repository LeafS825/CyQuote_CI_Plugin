using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace CyQuotePlugin.Services;

/// <summary>用真实的 Avalonia 排版引擎数行数，供 <see cref="QuoteLayout"/> 的回调使用。</summary>
public static class QuoteTextLayout
{
    /// <summary>
    /// 排下 text 需要几行；放不下时返回 maxLines + 1。
    /// 关键：除了 <see cref="TextLine.HasCollapsed"/>，还必须确认最后一行真的排到了文本末尾——
    /// MaxLines 截断时 HasCollapsed 未必为 true，只看它会漏掉"署名尾巴被砍却报放得下"。
    /// </summary>
    public static int CountLines(string text, double fontSize, double maxWidth, int maxLines, FontFamily? fontFamily = null)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // 没有宽度上限时永远是一行，也就无从判断溢出
        if (!double.IsFinite(maxWidth) || maxWidth <= 0) return 1;

        maxLines = Math.Max(1, maxLines);
        var family = fontFamily ?? FontManager.Current.DefaultFontFamily;
        var layout = new TextLayout(
            text,
            new Typeface(family),
            fontSize,
            Brushes.Transparent,
            TextAlignment.Center,
            TextWrapping.Wrap,
            TextTrimming.CharacterEllipsis,
            null,
            FlowDirection.LeftToRight,
            maxWidth,
            double.PositiveInfinity,
            0,
            0,
            maxLines,
            null);

        var lines = layout.TextLines;
        if (lines.Count == 0) return 0;

        var last = lines[^1];
        var covered = last.FirstTextSourceIndex + last.Length >= text.Length;
        var collapsed = lines.Any(line => line.HasCollapsed);
        if (!covered || collapsed) return maxLines + 1;
        return lines.Count;
    }
}
