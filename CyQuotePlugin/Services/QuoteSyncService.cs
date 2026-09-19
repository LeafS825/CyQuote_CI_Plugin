using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CyQuotePlugin.Services;

public sealed record QuoteSyncResult(bool Success, string Message, DateTimeOffset? CompletedAt = null);

public sealed class QuoteSyncService : INotifyPropertyChanged, IDisposable
{
    public const string DatasetUrl =
        "https://raw.githubusercontent.com/Cyrene2008/CyTime/Cyrene/api/data/quotes.jsonc";

    private readonly QuoteCatalog _catalog;
    private readonly HttpClient _client;
    private readonly string _activePath;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _syncLock = new();
    private Task<QuoteSyncResult>? _syncTask;
    private bool _isSyncing;
    private string _status = "尚未同步";

    public QuoteSyncService(QuoteCatalog catalog, HttpClient client, string activePath)
    {
        _catalog = catalog;
        _client = client;
        _activePath = activePath;
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        private set => SetField(ref _isSyncing, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task<QuoteSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (_syncTask is { IsCompleted: false }) return _syncTask;
            _syncTask = SyncCoreAsync(cancellationToken);
            return _syncTask;
        }
    }

    private async Task<QuoteSyncResult> SyncCoreAsync(CancellationToken cancellationToken)
    {
        IsSyncing = true;
        Status = "正在同步";
        var tempPath = _activePath + ".tmp";
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        try
        {
            var bytes = await _client.GetByteArrayAsync(DatasetUrl, linked.Token);
            var snapshot = QuoteCatalog.ParseUtf8(bytes);
            if (snapshot.Count == 0) throw new InvalidDataException("下载的语录库为空。");

            Directory.CreateDirectory(Path.GetDirectoryName(_activePath)!);
            await File.WriteAllBytesAsync(tempPath, bytes, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            // File replacement and snapshot publication form the non-cancellable commit phase.
            File.Move(tempPath, _activePath, true);
            _catalog.Publish(snapshot);
            var completedAt = DateTimeOffset.Now;
            Status = $"上次同步：{completedAt:yyyy-MM-dd HH:mm}";
            return new QuoteSyncResult(true, Status, completedAt);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            Status = "同步已取消";
            return new QuoteSyncResult(false, Status);
        }
        catch (Exception ex)
        {
            Status = $"同步失败：{ex.Message}";
            return new QuoteSyncResult(false, Status);
        }
        finally
        {
            IsSyncing = false;
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    public void Cancel() => _shutdown.Cancel();

    public void Dispose()
    {
        Cancel();
        _shutdown.Dispose();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        if (PropertyChanged is null) return;
        foreach (PropertyChangedEventHandler handler in PropertyChanged.GetInvocationList())
        {
            try
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
            catch
            {
                // UI observers cannot be allowed to break synchronization state.
            }
        }
    }
}
