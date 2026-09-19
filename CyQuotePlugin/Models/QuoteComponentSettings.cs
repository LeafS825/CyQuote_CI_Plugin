using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CyQuotePlugin.Models;

public sealed class QuoteComponentSettings : INotifyPropertyChanged
{
    private List<string> _selectedCategories = [];
    private int _changeIntervalSeconds = 60;
    private bool _showAuthor = true;
    private bool _showSource = true;
    private bool _singleLine;

    public List<string> SelectedCategories
    {
        get => _selectedCategories;
        set => SetField(ref _selectedCategories, value ?? []);
    }

    public int ChangeIntervalSeconds
    {
        get => _changeIntervalSeconds;
        set => SetField(ref _changeIntervalSeconds, value <= 0 ? 0 : Math.Clamp(value, 5, 86400));
    }

    public bool ShowAuthor
    {
        get => _showAuthor;
        set => SetField(ref _showAuthor, value);
    }

    public bool ShowSource
    {
        get => _showSource;
        set => SetField(ref _showSource, value);
    }

    public bool SingleLine
    {
        get => _singleLine;
        set => SetField(ref _singleLine, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
