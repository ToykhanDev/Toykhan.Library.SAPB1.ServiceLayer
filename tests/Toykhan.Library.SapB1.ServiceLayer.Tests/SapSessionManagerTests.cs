using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toykhan.Library.SapB1.ServiceLayer;
using Toykhan.Library.SapB1.ServiceLayer.Internal;

namespace Toykhan.Library.SapB1.ServiceLayer.Tests;

public sealed class SapSessionManagerTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly IDistributedCache _cache;
    private readonly ISapErrorTranslator _errorTranslator;
    private readonly IOptions<SapServiceLayerOptions> _options;

    public SapSessionManagerTests()
    {
        _mockHandler = new MockHttpMessageHandler();

        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHttpClient("sapb1")
            .ConfigurePrimaryHttpMessageHandler(() => _mockHandler);

        _serviceProvider = services.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        _errorTranslator = new SapErrorTranslator();
        _options = Options.Create(new SapServiceLayerOptions());
    }

    private SapSessionManager CreateManager() => new(
        _cache,
        _serviceProvider.GetRequiredService<IHttpClientFactory>(),
        _options,
        _errorTranslator,
        NullLogger<SapSessionManager>.Instance);

    private static SapConnectionContext CreateCtx() => new()
    {
        DataSourceKey = "test-dsk",
        BaseUrl = "https://localhost:50000/b1s/v2",
        CompanyDb = "test-db",
        UserName = "admin",
        Password = "p@ssw0rd"
    };

    [Fact]
    public async Task GetOrRefreshAsync_NoCachedSession_PerformsLogin()
    {
        _mockHandler.SetupLogin(sessionId: "SID-001", sessionTimeout: 30);
        var manager = CreateManager();
        var ctx = CreateCtx();

        var cookieHeader = await manager.GetOrRefreshAsync(ctx);

        Assert.Contains("B1SESSION=SID-001", cookieHeader);
        Assert.Equal(1, _mockHandler.LoginCallCount);
    }

    [Fact]
    public async Task GetOrRefreshAsync_CachedSession_DoesNotLogin()
    {
        _mockHandler.SetupLogin(sessionId: "SID-002", sessionTimeout: 30);
        var manager = CreateManager();
        var ctx = CreateCtx();

        // İlk çağrı: login yapar
        await manager.GetOrRefreshAsync(ctx);
        // İkinci çağrı: cache'ten döner
        var result = await manager.GetOrRefreshAsync(ctx);

        Assert.Equal(1, _mockHandler.LoginCallCount);
        Assert.Contains("SID-002", result);
    }

    [Fact]
    public async Task ForceReLoginAsync_InvalidatesThenLogsIn()
    {
        _mockHandler.SetupLogin(sessionId: "SID-003", sessionTimeout: 30);
        var manager = CreateManager();
        var ctx = CreateCtx();

        await manager.GetOrRefreshAsync(ctx); // ilk login
        await manager.ForceReLoginAsync(ctx); // zorla yeniden login

        Assert.Equal(2, _mockHandler.LoginCallCount);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedSession()
    {
        _mockHandler.SetupLogin(sessionId: "SID-004", sessionTimeout: 30);
        var manager = CreateManager();
        var ctx = CreateCtx();

        await manager.GetOrRefreshAsync(ctx);
        await manager.InvalidateAsync(ctx);
        await manager.GetOrRefreshAsync(ctx); // cache boş → tekrar login

        Assert.Equal(2, _mockHandler.LoginCallCount);
    }

    [Fact]
    public async Task ConcurrentRequests_OnlyOneLoginPerformed()
    {
        _mockHandler.SetupLogin(sessionId: "SID-005", sessionTimeout: 30, delayMs: 50);
        var manager = CreateManager();
        var ctx = CreateCtx();

        // 10 eşzamanlı istek — yalnızca 1 login bekleniyor
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => manager.GetOrRefreshAsync(ctx))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, _mockHandler.LoginCallCount);
    }

    public void Dispose() => _serviceProvider.Dispose();
}

// ─── Test double ────────────────────────────────────────────────────────────

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private int _loginCallCount;
    private string _sessionId = "TEST-SESSION";
    private int _sessionTimeout = 30;
    private int _delayMs = 0;

    public int LoginCallCount => _loginCallCount;

    public void SetupLogin(string sessionId, int sessionTimeout, int delayMs = 0)
    {
        _sessionId = sessionId;
        _sessionTimeout = sessionTimeout;
        _delayMs = delayMs;
        _loginCallCount = 0;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_delayMs > 0)
            await Task.Delay(_delayMs, cancellationToken);

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (path.EndsWith("/Login", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _loginCallCount);
            var loginResp = new { SessionId = _sessionId, Version = "9.30", SessionTimeout = _sessionTimeout };
            var json = JsonSerializer.Serialize(loginResp);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            response.Headers.Add("Set-Cookie", $"B1SESSION={_sessionId}; Path=/; HttpOnly");
            response.Headers.Add("Set-Cookie", $"CompanyDB=TEST_DB; Path=/; HttpOnly");
            return response;
        }

        if (path.EndsWith("/Logout", StringComparison.OrdinalIgnoreCase))
            return new HttpResponseMessage(HttpStatusCode.NoContent);

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
