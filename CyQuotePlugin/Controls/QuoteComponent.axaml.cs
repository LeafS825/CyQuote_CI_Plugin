using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
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
    private readonly QuoteCatalog _catalog;
    private CancellationTokenSource? _timerCancellation;
    private Quote? _currentQuote;
    private string _quoteText = "暂无可用语录";
    private string _metadataText = "";
    private string _singleLineText = "暂无可用语录";

    public QuoteComponent(QuoteCatalog catalog)
    {
        _catalog = catalog;
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    public string QuoteText
    {
        get => _quoteText;
        private set => SetField(ref _quoteText, value);
    }

    public string MetadataText
    {
        get => _metadataText;
        private set
        {
            if (!SetField(ref _metadataText, value)) return;
            OnPropertyChanged(nameof(HasMetadata));
        }
    }

    public bool HasMetadata => !string.IsNullOrWhiteSpace(MetadataText);

    public string SingleLineText
    {
        get => _singleLineText;
        private set => SetField(ref _singleLineText, value);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Settings.PropertyChanged += OnSettingsChanged;
        _catalog.Changed += OnCatalogChanged;
        ReplaceQuote();
        RestartTimer();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        _catalog.Changed -= OnCatalogChanged;
        StopTimer();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuoteComponentSettings.ChangeIntervalSeconds)) RestartTimer();
        ReplaceQuote();
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
        QuoteText = quote?.Value ?? "暂无可用语录";
        SingleLineText = quote is null
            ? QuoteText
            : Quote.FormatSingleLine(quote.Value, quote.Author, quote.Source, Settings.ShowAuthor, Settings.ShowSource);
        MetadataText = quote is null
            ? ""
            : string.Join(" · ", new[]
            {
                Settings.ShowAuthor ? quote.Author : "",
                Settings.ShowSource ? quote.Source : ""
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
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
