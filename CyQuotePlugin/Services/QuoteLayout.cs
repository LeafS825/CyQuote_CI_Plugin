namespace CyQuotePlugin.Services;

/// <summary>
/// 语录排版决策：单行放得下就单行；放不下按可用高度降字号折行；仍放不下就在署名之前断句省略。
/// 与具体 UI 解耦——排版细节通过 countLines 回调注入，便于单元测试。
/// </summary>
public static class QuoteLayout
{
    /// <summary>行高与字号的比例（Avalonia TextBlock 默认行高约 1.45em）。</summary>
    public const double LineHeightRatio = 1.45;

    /// <summary>断点回退时停在这些句读之后，避免把句子切在半截。</summary>
    public const string SentencePunctuations = "，。；：！？、,.;:!?…—";

    /// <param name="BaseFontSize">主题给定的原始字号</param>
    /// <param name="MaxWidth">有效宽度上限（窗口宽 / 组件高级设置的最大宽度）</param>
    /// <param name="UsableHeight">可用高度；0 表示不限制</param>
    /// <param name="MinFontSize">降字号时的下限</param>
    /// <param name="MaxLines">最大行数</param>
    /// <param name="Adaptive">false = 只折行，不降字号也不截断</param>
    public sealed record Options(
        double BaseFontSize,
        double MaxWidth,
        double UsableHeight,
        double MinFontSize,
        int MaxLines,
        bool Adaptive = true);

    public sealed record Result(string Text, double FontSize, bool Truncated, int LineCount);

    /// <summary>
    /// 计算最终展示的文本与字号。
    /// </summary>
    /// <param name="body">语录正文</param>
    /// <param name="attribution">署名（作者 · 出处），任何情况下都不参与截断</param>
    /// <param name="options">排版参数</param>
    /// <param name="countLines">给定文本与字号，返回排下它需要的行数；放不下时返回 MaxLines + 1</param>
    public static Result Arrange(
        string body,
        string attribution,
        Options options,
        Func<string, double, int> countLines)
    {
        body ??= string.Empty;
        attribution = attribution?.Trim() ?? string.Empty;

        var baseFontSize = options.BaseFontSize > 0 ? options.BaseFontSize : 20;
        var maxLines = Math.Max(1, options.MaxLines);

        if (body.Length == 0 && attribution.Length == 0)
            return new Result(string.Empty, baseFontSize, false, 0);

        var full = Compose(body, attribution, body.Length);

        // 1) 单行放得下：保持原字号，不折行不截断
        var singleLine = countLines(full, baseFontSize);
        if (singleLine <= 1)
            return new Result(full, baseFontSize, false, 1);

        if (!options.Adaptive)
        {
            // 关掉自适应：只折行，字号和文本都不动
            return new Result(full, baseFontSize, false, Math.Max(singleLine, 0));
        }

        // 2) 在 [下限, 原字号] 里找最大的字号：不截断就排得进 maxLines，且块高不超预算。
        //    单行不受预算约束——单行是既有的观感，预算只用来管我们新加出来的行。
        var floor = options.MinFontSize > 0 ? options.MinFontSize : 1;
        var fittedFont = FindLargestFittingFont(
            full, baseFontSize, floor, maxLines, options.UsableHeight, countLines);
        if (fittedFont > 0)
            return new Result(full, fittedFont, false, countLines(full, fittedFont));

        // 3) 没有这样的字号 → 按预算取字号，再在署名之前找断点
        var target = options.UsableHeight > 0
            ? options.UsableHeight / (maxLines * LineHeightRatio)
            : baseFontSize;
        var fontSize = Math.Clamp(target, floor, baseFontSize);

        var fitted = countLines(full, fontSize);
        if (fitted <= maxLines)
            return new Result(full, fontSize, false, fitted);

        // 3) 仍放不下：在署名之前找断点，再回退到句读之后
        var keep = FindBreakpoint(body, attribution, fontSize, maxLines, countLines);
        keep = SnapToPunctuation(body, keep);
        var text = Compose(body, attribution, keep);
        return new Result(text, fontSize, true, countLines(text, fontSize));
    }

    /// <summary>
    /// 二分找最大的字号，使文本不截断就能排进 maxLines 且块高不超预算。
    /// 找不到返回 -1。单行不受高度预算约束（既有观感优先）。
    /// </summary>
    private static double FindLargestFittingFont(
        string text,
        double upper,
        double lower,
        int maxLines,
        double usableHeight,
        Func<string, double, int> countLines)
    {
        bool Fits(double fontSize)
        {
            var lines = countLines(text, fontSize);
            if (lines <= 0 || lines > maxLines) return false;
            if (lines == 1 || usableHeight <= 0) return true;
            return lines * LineHeightRatio * fontSize <= usableHeight + 1e-6;
        }

        if (Fits(upper)) return upper;
        if (!Fits(lower)) return -1;

        // 固定迭代次数：区间最多几十 px，24 次后误差 < 1e-6，字号稳定可测
        var lo = lower;
        var hi = upper;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2;
            if (Fits(mid)) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>keep == body.Length 表示不截断；否则在 keep 处接省略号，署名永远拼在最后。</summary>
    private static string Compose(string body, string attribution, int keep)
    {
        string head;
        if (keep >= body.Length) head = body;
        else if (keep <= 0) head = body.Length == 0 ? string.Empty : "…";
        else head = $"{body[..keep]}…";

        if (attribution.Length == 0) return head;
        if (head.Length == 0) return attribution;
        return $"{head} · {attribution}";
    }

    /// <summary>二分正文字数，找出让总行数不超限的最大断点；断点一定在署名之前。</summary>
    private static int FindBreakpoint(
        string body,
        string attribution,
        double fontSize,
        int maxLines,
        Func<string, double, int> countLines)
    {
        var lo = 0;
        var hi = Math.Max(0, body.Length - 1);
        var best = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (countLines(Compose(body, attribution, mid), fontSize) <= maxLines)
            {
                best = mid;
                lo = mid + 1;
            }
            else hi = mid - 1;
        }
        return best < 0 ? 0 : best;
    }

    /// <summary>把断点往前退到最近一个句读之后；没有句读就原样返回。</summary>
    public static int SnapToPunctuation(string body, int keep)
    {
        if (keep <= 0 || body.Length == 0) return Math.Max(0, keep);
        var stop = Math.Min(keep, body.Length);
        for (var i = stop - 1; i >= 0; i--)
            if (SentencePunctuations.IndexOf(body[i]) >= 0)
                return i + 1;
        return keep;
    }
}
