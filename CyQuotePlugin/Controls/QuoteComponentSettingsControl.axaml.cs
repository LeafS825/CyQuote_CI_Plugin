using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using CyQuotePlugin.Models;
using CyQuotePlugin.Services;

namespace CyQuotePlugin.Controls;

public partial class QuoteComponentSettingsControl : ComponentBase<QuoteComponentSettings>, INotifyPropertyChanged
{
    private readonly QuoteCatalog _catalog;
    private readonly QuoteSyncService _syncService;

    public QuoteComponentSettingsControl(QuoteCatalog catalog, QuoteSyncService syncService)
    {
        _catalog = catalog;
        _syncService = syncService;
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];
    public string CatalogSummary => $"{_catalog.Current.Categories.Count} 个分类，{_catalog.Current.Count} 条语录";
    public string SyncStatus => _syncService.Status;
    public bool CanSync => !_syncService.IsSyncing;

    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _catalog.Changed += OnCatalogChanged;
        _syncService.PropertyChanged += OnSyncPropertyChanged;
        RebuildCategories();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _catalog.Changed -= OnCatalogChanged;
        _syncService.PropertyChanged -= OnSyncPropertyChanged;
    }

    private void OnCatalogChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            RebuildCategories();
            OnPropertyChanged(nameof(CatalogSummary));
        });

    private void OnSyncPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(SyncStatus));
            OnPropertyChanged(nameof(CanSync));
        });

    private void RebuildCategories()
    {
        var selected = Settings.SelectedCategories.ToHashSet(StringComparer.Ordinal);
        CategoryOptions.Clear();
        foreach (var category in _catalog.Current.Categories)
            CategoryOptions.Add(new CategoryOption(category, selected.Contains(category), UpdateCategory));
    }

    private void UpdateCategory(string category, bool selected)
    {
        var categories = Settings.SelectedCategories.ToHashSet(StringComparer.Ordinal);
        if (selected) categories.Add(category);
        else categories.Remove(category);
        Settings.SelectedCategories = categories.Order(StringComparer.Ordinal).ToList();
    }

    private async void OnSyncClick(object? sender, RoutedEventArgs e) =>
        await _syncService.SyncAsync();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class CategoryOption : INotifyPropertyChanged
    {
        private readonly Action<string, bool> _changed;
        private bool _isSelected;

        public CategoryOption(string name, bool isSelected, Action<string, bool> changed)
        {
            Name = name;
            _isSelected = isSelected;
            _changed = changed;
        }

        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                _changed(Name, value);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
