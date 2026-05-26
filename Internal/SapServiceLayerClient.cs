using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Toykhan.Library.SapB1.ServiceLayer.Internal;

internal sealed class SapServiceLayerClient : ISapServiceLayerClient
{
    private const string HttpClientName = "sapb1";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISapErrorTranslator _errorTranslator;
    private readonly SapServiceLayerOptions _options;
    private readonly ILogger<SapServiceLayerClient> _logger;

    // HttpMethod.Patch netstandard2.0'da yok
    private static readonly HttpMethod PatchMethod = new HttpMethod("PATCH");

    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SapServiceLayerClient(
        IHttpClientFactory httpClientFactory,
        ISapErrorTranslator errorTranslator,
        IOptions<SapServiceLayerOptions> options,
        ILogger<SapServiceLayerClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _errorTranslator = errorTranslator;
        _options = options.Value;
        _logger = logger;
    }

    // ─── GET ───────────────────────────────────────────────────────────────────

    public async Task<List<T>> GetCollectionAsync<T>(
        SapConnectionContext ctx, string resource,
        SapODataQuery? query = null, CancellationToken ct = default)
    {
        var uri = BuildUri(ctx.BaseUrl, resource, query);
        using var response = await SendAsync(ctx, HttpMethod.Get, uri, body: null, ct);
        var result = await ReadJsonAsync<SapCollectionResult<T>>(response, ct);
        return result.Value;
    }

    public async Task<T> GetSingleAsync<T>(
        SapConnectionContext ctx, string resource, object id,
        SapODataQuery? query = null, CancellationToken ct = default)
    {
        var resourceWithId = FormatResourceId(resource, id);
        var uri = BuildUri(ctx.BaseUrl, resourceWithId, query);
        using var response = await SendAsync(ctx, HttpMethod.Get, uri, body: null, ct);
        return await ReadJsonAsync<T>(response, ct);
    }

    public async Task<(List<T> Items, int TotalCount)> GetWithInlineCountAsync<T>(
        SapConnectionContext ctx, string resource,
        SapODataQuery? query = null, CancellationToken ct = default)
    {
        var q = (query ?? SapODataQuery.New()).WithInlineCount();
        var uri = BuildUri(ctx.BaseUrl, resource, q);
        using var response = await SendAsync(ctx, HttpMethod.Get, uri, body: null, ct);
        var result = await ReadJsonAsync<SapCollectionResult<T>>(response, ct);
        return (result.Value, result.TotalCount ?? result.Value.Count);
    }

    public async Task<List<T>> GetAllPagesAsync<T>(
        SapConnectionContext ctx, string resource,
        SapODataQuery? query = null, CancellationToken ct = default)
    {
        var all = new List<T>();
        int pageSize = query?.PageSize ?? 20;
        int skip = query?.Skip ?? 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var pageQuery = (query ?? SapODataQuery.New())
                .WithTop(pageSize)
                .WithSkip(skip);

            var uri = BuildUri(ctx.BaseUrl, resource, pageQuery);
            using var response = await SendAsync(ctx, HttpMethod.Get, uri, body: null, ct);
            var result = await ReadJsonAsync<SapCollectionResult<T>>(response, ct);

            all.AddRange(result.Value);

            if (!result.HasNextPage || result.Value.Count == 0)
                break;

            skip += pageSize;
        }

        return all;
    }

    // ─── POST ──────────────────────────────────────────────────────────────────

    public async Task<T> PostAsync<T>(
        SapConnectionContext ctx, string resource, object data, CancellationToken ct = default)
    {
        var uri = BuildUri(ctx.BaseUrl, resource);
        using var response = await SendAsync(ctx, HttpMethod.Post, uri, data, ct);
        return await ReadJsonAsync<T>(response, ct);
    }

    public async Task PostNoContentAsync(
        SapConnectionContext ctx, string resource, object data, CancellationToken ct = default)
    {
        var uri = BuildUri(ctx.BaseUrl, resource);
        using var request = CreateRequest(ctx, HttpMethod.Post, uri, data);
        request.Headers.TryAddWithoutValidation("Prefer", "return-no-content");
        using var response = await ExecuteAsync(ctx, request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ─── PATCH ─────────────────────────────────────────────────────────────────

    public async Task PatchAsync(
        SapConnectionContext ctx, string resource, object id, object data, CancellationToken ct = default)
    {
        var uri = BuildUri(ctx.BaseUrl, FormatResourceId(resource, id));
        using var response = await SendAsync(ctx, PatchMethod, uri, data, ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ─── DELETE ────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(
        SapConnectionContext ctx, string resource, object id, CancellationToken ct = default)
    {
        var uri = BuildUri(ctx.BaseUrl, FormatResourceId(resource, id));
        using var response = await SendAsync(ctx, HttpMethod.Delete, uri, body: null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ─── PING ──────────────────────────────────────────────────────────────────

    public async Task<bool> PingAsync(SapConnectionContext ctx, CancellationToken ct = default)
    {
        try
        {
            var uri = BuildPingUri(ctx.BaseUrl);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // Ping için oturum gerekmez — context property koyma
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SAP Ping] Failed for {DataSourceKey}", ctx.DataSourceKey);
            return false;
        }
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(
        SapConnectionContext ctx,
        HttpMethod method,
        string uri,
        object? body,
        CancellationToken ct)
    {
        using var request = CreateRequest(ctx, method, uri, body);
        var response = await ExecuteAsync(ctx, request, ct);
        await EnsureSuccessAsync(response, ct);
        return response;
    }

    private async Task<HttpResponseMessage> ExecuteAsync(
        SapConnectionContext ctx,
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("[SAP] {Method} {Resource} @ {DataSourceKey} ({CompanyDb})",
            request.Method.Method,
            request.RequestUri?.PathAndQuery,
            ctx.DataSourceKey,
            ctx.CompanyDb);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        _logger.LogDebug("[SAP] {Method} {Resource} → {Status} in {Ms}ms",
            request.Method.Method,
            request.RequestUri?.PathAndQuery,
            (int)response.StatusCode,
            sw.ElapsedMilliseconds);

        return response;
    }

    private HttpRequestMessage CreateRequest(
        SapConnectionContext ctx,
        HttpMethod method,
        string uri,
        object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.ParseAdd("application/json");

#pragma warning disable CS0618
        request.Properties[SapAuthDelegatingHandler.ContextPropertyKey] = ctx;
#pragma warning restore CS0618

        if (body is not null)
        {
            var json = body is string s
                ? s
                : JsonSerializer.Serialize(body, _serializeOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        throw _errorTranslator.Translate(body);
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, _deserializeOptions)
            ?? throw new InvalidOperationException("SAP Service Layer returned a null response.");
    }

    private static string BuildUri(string baseUrl, string resource, SapODataQuery? query = null)
    {
        var url = $"{baseUrl.TrimEnd('/')}/{resource}";
        if (query is null) return url;

        var qs = BuildQueryString(query);
        return string.IsNullOrEmpty(qs) ? url : $"{url}?{qs}";
    }

    private static string BuildPingUri(string baseUrl)
    {
        // SAP ping endpoint — Service Layer root'un bir seviye üstünde
        var uri = new Uri(baseUrl.TrimEnd('/'));
        var scheme = uri.Scheme;
        var host = uri.Host;
        var port = uri.Port;
        return $"{scheme}://{host}:{port}/ping/";
    }

    private static string BuildQueryString(SapODataQuery query)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(query.Filter))
            parts.Add($"$filter={HttpUtility.UrlEncode(query.Filter)}");
        if (!string.IsNullOrEmpty(query.Select))
            parts.Add($"$select={HttpUtility.UrlEncode(query.Select)}");
        if (!string.IsNullOrEmpty(query.OrderBy))
            parts.Add($"$orderby={HttpUtility.UrlEncode(query.OrderBy)}");
        if (query.Top.HasValue)
            parts.Add($"$top={query.Top}");
        if (query.Skip.HasValue)
            parts.Add($"$skip={query.Skip}");
        if (!string.IsNullOrEmpty(query.Expand))
            parts.Add($"$expand={HttpUtility.UrlEncode(query.Expand)}");
        if (!string.IsNullOrEmpty(query.Apply))
            parts.Add($"$apply={HttpUtility.UrlEncode(query.Apply)}");
        if (query.InlineCount)
            parts.Add("$inlinecount=allpages");

        return string.Join("&", parts);
    }

    /// <summary>
    /// String ID için tek tırnaklı, sayısal ID için sayısal OData kaynak adresi üretir.
    /// <br/>Örn: <c>BusinessPartners('C001')</c> veya <c>Orders(123)</c>
    /// </summary>
    private static string FormatResourceId(string resource, object id)
    {
        return id switch
        {
            string s => $"{resource}('{s}')",
            _ => $"{resource}({id})"
        };
    }
}
