using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using CyQuotePlugin.Models;
using CyQuotePlugin.Services;

namespace CyQuotePlugin.Controls;

[ComponentInfo(
    "6F5776CB-1D19-48A2-87B4-84768BA6A43A",
    "昔言语录",
    "\uE8D2",
    "从本地昔言语录库随机展示内容。")]
public partial class QuoteComponent : ComponentBase<QuoteComponentSettings>, INotifyPropertyChanged
{
    /// <summary>窗口左右各留的边距，用于由窗口宽度推出语录的可用宽度。</summary>
    private const double WindowSideMargin = 48;

    /// <summary>资源缺失时的兜底字号。</summary>
    private const double FallbackFontSize = 20;

    private const string FallbackText = "暂无可用语录";

    private readonly QuoteCatalog _catalog;
    private CancellationTokenSource? _timerCancellation;
    private Quote? _currentQuote;
    private ComponentPresenter? _hostPresenter;

    private string _displayText = FallbackText;
    private double _displayFontSize = FallbackFontSize;
    private double _displayMaxWidth = double.PositiveInfinity;
    private TextWrapping _displayWrapping = TextWrapping.Wrap;
    private int _displayMaxLines = 2;
    private TextTrimming _displayTrimming = TextTrimming.CharacterEllipsis;

    private double _baseFontSize = FallbackFontSize;
    private double _maxWidth = double.PositiveInfinity;
    private double _appliedMaxWidth = double.NaN;
    private double? _cachedBaseFontSize;

    public QuoteComponent(QuoteCatalog catalog)
    {
        _catalog = catalog;
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        SizeChanged += (_, _) => RefreshIfWidthChanged();
    }

    public string DisplayText
    {
        get => _displayText;
        private set => SetField(ref _displayText, value);
    }

    public double DisplayFontSize
    {
        get => _displayFontSize;
        private set => SetField(ref _displayFontSize, value);
    }

    /// <summary>必须有限才有意义：ClassIsland 行内组件按无限宽测量，没有宽度上限时换行与省略号都不会生效。</summary>
    public double DisplayMaxWidth
    {
        get => _displayMaxWidth;
        private set => SetField(ref _displayMaxWidth, value);
    }

    public TextWrapping DisplayWrapping
    {
        get => _displayWrapping;
        private set => SetField(ref _displayWrapping, value);
    }

    public int DisplayMaxLines
    {
        get => _displayMaxLines;
        private set => SetField(ref _displayMaxLines, value);
    }

    public TextTrimming DisplayTrimming
    {
        get => _displayTrimming;
        private set => SetField(ref _displayTrimming, value);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Settings.PropertyChanged += OnSettingsChanged;
        _catalog.Changed += OnCatalogChanged;
        AttachHostPresenter();
        ReplaceQuote();
        RestartTimer();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        _catalog.Changed -= OnCatalogChanged;
        DetachHostPresenter();
        StopTimer();
        _cachedBaseFontSize = null;
        _appliedMaxWidth = double.NaN;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuoteComponentSettings.ChangeIntervalSeconds)) RestartTimer();
        if (e.PropertyName == nameof(QuoteComponentSettings.SelectedCategories))
        {
            // 换了分类要重新抽一条
            ReplaceQuote();
            return;
        }
        // 显示作者 / 字号 / 行数等只影响排版，不必重抽语录
        ApplyLayout();
    }

    private void OnCatalogChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ReplaceQuote);

    private void RestartTimer()
    {
        StopTimer();
        if (Settings.ChangeIntervalSeconds == 0) return;
        _timerCancellation = new CancellationTokenSource();
        _ = RunTimerAsync(_timerCancellation.Token);
    }

    private void StopTimer()
    {
        _timerCancellation?.Cancel();
        _timerCancellation?.Dispose();
        _timerCancellation = null;
    }

    private async Task RunTimerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Settings.ChangeIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                Dispatcher.UIThread.Post(ReplaceQuote);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ReplaceQuote()
    {
        var quote = _catalog.Pick(Settings.SelectedCategories);
        if (quote == _currentQuote && _catalog.Current.Count > 1)
            quote = _catalog.Pick(Settings.SelectedCategories);

        _currentQuote = quote;
        ApplyLayout();
    }

    /// <summary>把当前语录按有效宽度排成最终展示的文本与字号。</summary>
    private void ApplyLayout()
    {
        var body = _currentQuote?.Value ?? FallbackText;
        var attribution = BuildAttribution(_currentQuote);

        _baseFontSize = ResolveBaseFontSize();
        _maxWidth = ResolveMaxWidth();
        _appliedMaxWidth = _maxWidth;
        DisplayMaxWidth = double.IsFinite(_maxWidth) && _maxWidth > 0 ? _maxWidth : double.PositiveInfinity;

        if (Settings.SingleLine)
        {
            // 强制单行：正文与署名同一行，超出用省略号收住，不再画出屏幕
            DisplayText = _currentQuote is null
                ? body
                : Quote.FormatSingleLine(
                    _currentQuote.Value, _currentQuote.Author, _currentQuote.Source,
                    Settings.ShowAuthor, Settings.ShowSource);
            DisplayFontSize = _baseFontSize;
            DisplayWrapping = TextWrapping.NoWrap;
            DisplayMaxLines = 1;
            DisplayTrimming = TextTrimming.CharacterEllipsis;
            return;
        }

        var options = new QuoteLayout.Options(
            BaseFontSize: _baseFontSize,
            MaxWidth: _maxWidth,
            UsableHeight: Settings.MaxBlockHeight,
            MinFontSize: Settings.MinFontSize,
            MaxLines: Settings.MaxLines,
            Adaptive: Settings.AdaptiveLayout);

        var result = QuoteLayout.Arrange(body, attribution, options, CountLines);

        DisplayText = result.Text;
        DisplayFontSize = result.FontSize;
        DisplayWrapping = TextWrapping.Wrap;
        // 关掉自适应时只折行、不封行也不截断
        DisplayMaxLines = Settings.AdaptiveLayout ? Math.Max(1, Settings.MaxLines) : 0;
        DisplayTrimming = Settings.AdaptiveLayout ? TextTrimming.CharacterEllipsis : TextTrimming.None;
    }

    /// <summary>
    /// 排下 text 需要几行；放不下时返回 MaxLines + 1。
    /// 交给 <see cref="QuoteTextLayout"/>，判定逻辑（含 MaxLines 截断的覆盖校验）在那里统一实现并有回归测试。
    /// </summary>
    private int CountLines(string text, double fontSize) =>
        QuoteTextLayout.CountLines(
            text,
            fontSize,
            _maxWidth,
            Settings.MaxLines,
            QuoteTextBlock?.FontFamily);

    private string BuildAttribution(Quote? quote)
    {
        if (quote is null) return string.Empty;
        return string.Join(" · ", new[]
        {
            Settings.ShowAuthor ? quote.Author : "",
            Settings.ShowSource ? quote.Source : ""
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    /// <summary>主题给定的强调字号；缓存到本次挂载结束，避免拿到自己降过的字号当基准。</summary>
    private double ResolveBaseFontSize()
    {
        if (_cachedBaseFontSize is > 0) return _cachedBaseFontSize.Value;

        var resolved = FallbackFontSize;
        if (this.TryFindResource("MainWindowEmphasizedFontSize", out var value) &&
            value is double candidate && candidate > 0)
            resolved = candidate;

        _cachedBaseFontSize = resolved;
        return resolved;
    }

    /// <summary>有效宽度上限 = min(窗口宽 − 边距, 高级设置里的固定宽度 / 最大宽度)。</summary>
    private double ResolveMaxWidth()
    {
        var limit = double.PositiveInfinity;

        if (VisualRoot is TopLevel topLevel && topLevel.Bounds.Width > 0)
            limit = topLevel.Bounds.Width - WindowSideMargin;

        var host = _hostPresenter?.Settings;
        if (host != null)
        {
            if (host.IsFixedWidthEnabled && host.FixedWidth > 0) limit = Math.Min(limit, host.FixedWidth);
            if (host.IsMaxWidthEnabled && host.MaxWidth > 0) limit = Math.Min(limit, host.MaxWidth);
        }

        return limit;
    }

    /// <summary>窗口宽度或高级设置的最大宽度变了才重排，避免自己的尺寸变化触发无谓循环。</summary>
    private void RefreshIfWidthChanged()
    {
        var width = ResolveMaxWidth();
        if (double.IsNaN(_appliedMaxWidth) || Math.Abs(width - _appliedMaxWidth) > 0.5)
            ApplyLayout();
    }

    private void AttachHostPresenter()
    {
        _hostPresenter = this.GetVisualAncestors().OfType<ComponentPresenter>().FirstOrDefault();
        if (_hostPresenter?.Settings is { } hostSettings)
            hostSettings.PropertyChanged += OnHostSettingsChanged;
    }

    private void DetachHostPresenter()
    {
        if (_hostPresenter?.Settings is { } hostSettings)
            hostSettings.PropertyChanged -= OnHostSettingsChanged;
        _hostPresenter = null;
    }

    private void OnHostSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClassIsland.Core.Models.Components.ComponentSettings.MaxWidth)
            or nameof(ClassIsland.Core.Models.Components.ComponentSettings.FixedWidth)
            or nameof(ClassIsland.Core.Models.Components.ComponentSettings.IsMaxWidthEnabled)
            or nameof(ClassIsland.Core.Models.Components.ComponentSettings.IsFixedWidthEnabled))
        {
            Dispatcher.UIThread.Post(ApplyLayout);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ReplaceQuote();
        e.Handled = true;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
