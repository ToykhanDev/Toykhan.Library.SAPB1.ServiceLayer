using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Toykhan.Library.SapB1.ServiceLayer.Internal;

internal sealed class SapSessionManager : ISapSessionManager, IDisposable
{
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SapServiceLayerOptions _options;
    private readonly ISapErrorTranslator _errorTranslator;
    private readonly ILogger<SapSessionManager> _logger;

    // Datasource başına semaphore — login storm koruması
    private readonly Dictionary<string, SemaphoreSlim> _locks = new();
    private readonly object _lockRegistry = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SapSessionManager(
        IDistributedCache cache,
        IHttpClientFactory httpClientFactory,
        IOptions<SapServiceLayerOptions> options,
        ISapErrorTranslator errorTranslator,
        ILogger<SapSessionManager> logger)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _errorTranslator = errorTranslator;
        _logger = logger;
    }

    public async Task<string> GetOrRefreshAsync(SapConnectionContext ctx, CancellationToken ct = default)
    {
        var cachedBytes = await _cache.GetAsync(ctx.SessionCacheKey, ct);
        if (cachedBytes is { Length: > 0 })
        {
            await _cache.RefreshAsync(ctx.SessionCacheKey, ct);
            var state = JsonSerializer.Deserialize<SapSessionState>(cachedBytes)!;
            _logger.LogDebug("[SAP Session] Cache hit for {DataSourceKey} ({CompanyDb})",
                ctx.DataSourceKey, ctx.CompanyDb);
            return state.CookieHeader;
        }

        return await PerformLoginAsync(ctx, ct);
    }

    public async Task<string> ForceReLoginAsync(SapConnectionContext ctx, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(ctx.SessionCacheKey, ct);
        _logger.LogDebug("[SAP Session] Forced re-login for {DataSourceKey} ({CompanyDb})",
            ctx.DataSourceKey, ctx.CompanyDb);
        return await PerformLoginAsync(ctx, ct);
    }

    public async Task InvalidateAsync(SapConnectionContext ctx, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(ctx.SessionCacheKey, ct);
        _logger.LogDebug("[SAP Session] Invalidated session for {DataSourceKey}", ctx.DataSourceKey);
    }

    public async Task LogoutAsync(SapConnectionContext ctx, CancellationToken ct = default)
    {
        var cachedBytes = await _cache.GetAsync(ctx.SessionCacheKey, ct);
        if (cachedBytes is not { Length: > 0 })
        {
            _logger.LogDebug("[SAP Session] Logout skipped — no active session for {DataSourceKey}", ctx.DataSourceKey);
            return;
        }

        var state = JsonSerializer.Deserialize<SapSessionState>(cachedBytes)!;
        try
        {
            using var client = _httpClientFactory.CreateClient(ctx.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(ctx.BaseUrl, "Logout"));
            request.Headers.TryAddWithoutValidation("Cookie", state.CookieHeader);
            await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SAP Session] Logout request failed for {DataSourceKey} — session will be invalidated locally", ctx.DataSourceKey);
        }
        finally
        {
            await _cache.RemoveAsync(ctx.SessionCacheKey, ct);
        }
    }

    // ─── Private ───────────────────────────────────────────────────────────────

    private async Task<string> PerformLoginAsync(SapConnectionContext ctx, CancellationToken ct)
    {
        var semaphore = GetOrCreateSemaphore(ctx.SessionCacheKey);
        await semaphore.WaitAsync(ct);
        try
        {
            // Semaphore beklerken başka bir thread login yapmış olabilir
            var raceBytes = await _cache.GetAsync(ctx.SessionCacheKey, ct);
            if (raceBytes is { Length: > 0 })
            {
                var existing = JsonSerializer.Deserialize<SapSessionState>(raceBytes)!;
                _logger.LogDebug("[SAP Session] Login storm guard hit for {DataSourceKey}", ctx.DataSourceKey);
                return existing.CookieHeader;
            }

            _logger.LogInformation("[SAP Session] Logging in: {DataSourceKey} ({CompanyDb}) @ {BaseUrl}",
                ctx.DataSourceKey, ctx.CompanyDb, ctx.BaseUrl);

            // Login isteği doğrudan "sapb1" isimli client ile yapılır.
            // SapAuthDelegatingHandler'ın cookie enjeksiyonu devreye girmesin diye
            // HttpRequestMessage'a context property eklenmez.
            using var client = _httpClientFactory.CreateClient("sapb1");
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(ctx.BaseUrl, "Login"));
            request.Headers.Accept.ParseAdd("application/json");

            var loginBody = new
            {
                CompanyDB = ctx.CompanyDb,
                UserName = ctx.UserName,
                Password = ctx.Password,
                Language = ctx.Language
            };
            request.Content = new StringContent(
                JsonSerializer.Serialize(loginBody, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw _errorTranslator.Translate(errorBody);
            }

            // Cookie'leri Set-Cookie header'larından topla
            var cookieHeader = ExtractCookieHeader(response);

            var responseBody = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<SapLoginResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var sessionState = new SapSessionState
            {
                SessionId = loginResponse?.SessionId ?? string.Empty,
                SboVersion = loginResponse?.Version ?? string.Empty,
                SessionTimeoutMinutes = loginResponse?.SessionTimeout > 0
                    ? loginResponse.SessionTimeout
                    : _options.DefaultSessionTimeoutMinutes,
                LoginAtUtc = DateTime.UtcNow,
                CookieHeader = cookieHeader
            };

            var cacheBytes = JsonSerializer.SerializeToUtf8Bytes(sessionState);
            await _cache.SetAsync(ctx.SessionCacheKey, cacheBytes, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(sessionState.SessionTimeoutMinutes)
            }, ct);

            _logger.LogInformation("[SAP Session] Login successful: {DataSourceKey}, version={SboVersion}, timeout={Timeout}m",
                ctx.DataSourceKey, sessionState.SboVersion, sessionState.SessionTimeoutMinutes);

            return cookieHeader;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static string ExtractCookieHeader(HttpResponseMessage response)
    {
        var cookies = new List<string>();

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            foreach (var raw in setCookieValues)
            {
                // Her Set-Cookie satırından yalnızca "Name=Value" kısmını al
                var nameValue = raw.Split(';')[0].Trim();
                if (!string.IsNullOrEmpty(nameValue))
                    cookies.Add(nameValue);
            }
        }

        return string.Join("; ", cookies);
    }

    private static string BuildUri(string baseUrl, string path)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}/{path}";
    }

    private SemaphoreSlim GetOrCreateSemaphore(string key)
    {
        lock (_lockRegistry)
        {
            if (!_locks.TryGetValue(key, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _locks[key] = semaphore;
            }
            return semaphore;
        }
    }

    public void Dispose()
    {
        lock (_lockRegistry)
        {
            foreach (var sem in _locks.Values)
                sem.Dispose();
            _locks.Clear();
        }
    }
}
