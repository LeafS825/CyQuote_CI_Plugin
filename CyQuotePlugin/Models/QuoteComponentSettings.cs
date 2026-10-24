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
    private bool _adaptiveLayout = true;
    private int _maxLines = 2;
    private double _minFontSize = 14;
    private double _maxBlockHeight = 45;

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

    /// <summary>
    /// 自适应排版：单行放不下时按可用高度降字号折行，仍放不下就在署名之前断句省略。
    /// 关闭后只折行，不降字号也不截断。
    /// </summary>
    public bool AdaptiveLayout
    {
        get => _adaptiveLayout;
        set => SetField(ref _adaptiveLayout, value);
    }

    /// <summary>最大行数，1-4。</summary>
    public int MaxLines
    {
        get => _maxLines;
        set => SetField(ref _maxLines, Math.Clamp(value, 1, 4));
    }

    /// <summary>降字号的下限，6-72。</summary>
    public double MinFontSize
    {
        get => _minFontSize;
        set => SetField(ref _minFontSize, Math.Clamp(value, 6, 72));
    }

    /// <summary>
    /// 排版可用高度（px），0 表示不限制。
    /// ClassIsland 主界面行的组件容器上限是 40px、行距 5px，故默认 45。
    /// </summary>
    public double MaxBlockHeight
    {
        get => _maxBlockHeight;
        set => SetField(ref _maxBlockHeight, Math.Clamp(value, 0, 500));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
