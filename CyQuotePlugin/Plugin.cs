using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using CyQuotePlugin.Controls;
using CyQuotePlugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CyQuotePlugin;

[PluginEntrance]
public class Plugin : PluginBase
{
    private QuoteSyncService? _syncService;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var pluginFolder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!;
        var bundledPath = Path.Combine(pluginFolder, "Assets", "quotes.jsonc");
        var activePath = Path.Combine(PluginConfigFolder, "quotes.jsonc");
        Directory.CreateDirectory(PluginConfigFolder);
        if (!File.Exists(activePath)) File.Copy(bundledPath, activePath);

        var catalog = new QuoteCatalog();
        try
        {
            catalog.Publish(QuoteCatalog.ParseUtf8(File.ReadAllBytes(activePath)));
        }
        catch
        {
            catalog.Publish(QuoteCatalog.ParseUtf8(File.ReadAllBytes(bundledPath)));
        }

        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _syncService = new QuoteSyncService(catalog, httpClient, activePath);
        services.AddSingleton(catalog);
        services.AddSingleton(httpClient);
        services.AddSingleton(_syncService);
        services.AddComponent<QuoteComponent, QuoteComponentSettingsControl>();

        AppBase.Current.AppStarted += OnAppStarted;
        AppBase.Current.AppStopping += OnAppStopping;
    }

    private async void OnAppStarted(object? sender, EventArgs e)
    {
        if (_syncService is not null) await _syncService.SyncAsync();
    }

    private void OnAppStopping(object? sender, EventArgs e) => _syncService?.Cancel();
}
