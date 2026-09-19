using System.Net;
using System.Text;
using CyQuotePlugin.Services;

namespace CyQuotePlugin.Tests;

public class QuoteSyncServiceTests
{
    private const string Original = """{ "A": [{ "value": "old" }] }""";

    [Fact]
    public async Task SyncAsync_InvalidDownloadPreservesFileAndCatalog()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var activePath = Path.Combine(folder.FullName, "quotes.jsonc");
            await File.WriteAllTextAsync(activePath, Original);
            var catalog = new QuoteCatalog();
            catalog.Publish(QuoteCatalog.Parse(Original));
            using var client = new HttpClient(new StubHandler("not json"));
            using var service = new QuoteSyncService(catalog, client, activePath);

            var result = await service.SyncAsync();

            Assert.False(result.Success);
            Assert.Equal(Original, await File.ReadAllTextAsync(activePath));
            Assert.Equal("old", catalog.Pick([], new Random(1))!.Value);
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Fact]
    public async Task SyncAsync_ValidDownloadReplacesFileAndCatalog()
    {
        const string updated = """{ "B": [{ "value": "new" }] }""";
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var activePath = Path.Combine(folder.FullName, "quotes.jsonc");
            await File.WriteAllTextAsync(activePath, Original);
            var catalog = new QuoteCatalog();
            catalog.Publish(QuoteCatalog.Parse(Original));
            using var client = new HttpClient(new StubHandler(updated));
            using var service = new QuoteSyncService(catalog, client, activePath);

            var result = await service.SyncAsync();

            Assert.True(result.Success);
            Assert.Equal(updated, await File.ReadAllTextAsync(activePath));
            Assert.Equal("new", catalog.Pick([], new Random(1))!.Value);
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Fact]
    public async Task SyncAsync_InvalidUtf8PreservesFileAndCatalog()
    {
        var corrupt = Encoding.UTF8.GetBytes("""{ "A": [{ "value": "quote" }] }""");
        corrupt[20] = 0xff;
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var activePath = Path.Combine(folder.FullName, "quotes.jsonc");
            await File.WriteAllTextAsync(activePath, Original);
            var catalog = new QuoteCatalog();
            catalog.Publish(QuoteCatalog.Parse(Original));
            using var client = new HttpClient(new StubHandler(corrupt));
            using var service = new QuoteSyncService(catalog, client, activePath);

            var result = await service.SyncAsync();

            Assert.False(result.Success);
            Assert.Equal(Original, await File.ReadAllTextAsync(activePath));
            Assert.Equal("old", catalog.Pick([], new Random(1))!.Value);
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Fact]
    public async Task SyncAsync_PropertyChangedSubscriberFailureDoesNotFaultOrStick()
    {
        const string updated = """{ "B": [{ "value": "new" }] }""";
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var activePath = Path.Combine(folder.FullName, "quotes.jsonc");
            await File.WriteAllTextAsync(activePath, Original);
            var catalog = new QuoteCatalog();
            catalog.Publish(QuoteCatalog.Parse(Original));
            using var client = new HttpClient(new StubHandler(updated));
            using var service = new QuoteSyncService(catalog, client, activePath);
            service.PropertyChanged += (_, _) => throw new InvalidOperationException("subscriber failed");

            var result = await service.SyncAsync();

            Assert.True(result.Success);
            Assert.False(service.IsSyncing);
            Assert.Equal("new", catalog.Pick([], new Random(1))!.Value);
        }
        finally
        {
            folder.Delete(true);
        }
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        private readonly byte[] _content = Encoding.UTF8.GetBytes(content);

        public StubHandler(byte[] content) : this("") => _content = content;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_content)
        });
    }
}
