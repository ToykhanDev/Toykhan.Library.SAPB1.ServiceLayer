using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Toykhan.Library.SapB1.ServiceLayer.Internal;

/// <summary>
/// HTTP pipeline delegating handler'ı.
/// <para>
/// Her istekte geçerli oturum cookie'sini ekler.
/// 401 Unauthorized yanıtı alındığında oturumu yeniler ve isteği bir kez daha gönderir.
/// </para>
/// <para>
/// <see cref="SapConnectionContext"/>'i <see cref="HttpRequestMessage.Properties"/> üzerinden alır.
/// <see cref="SapServiceLayerClient"/> gönderimden önce context'i properties'a yazar.
/// </para>
/// </summary>
internal sealed class SapAuthDelegatingHandler : DelegatingHandler
{
    internal const string ContextPropertyKey = "sapb1:connection-context";

    private readonly ISapSessionManager _sessionManager;
    private readonly ILogger<SapAuthDelegatingHandler> _logger;

    public SapAuthDelegatingHandler(
        ISapSessionManager sessionManager,
        ILogger<SapAuthDelegatingHandler> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // HttpRequestMessage.Properties is obsolete in .NET 5+ but needed for netstandard2.0
        if (!request.Properties.TryGetValue(ContextPropertyKey, out var ctxObj) ||
            ctxObj is not SapConnectionContext ctx)
        {
            // Context yoksa doğrudan ilet (ping gibi login gerektirmeyen istekler)
            return await base.SendAsync(request, cancellationToken);
        }
#pragma warning restore CS0618

        // İlk deneme — mevcut oturum cookie'siyle
        var cookieHeader = await _sessionManager.GetOrRefreshAsync(ctx, cancellationToken);
        SetCookie(request, cookieHeader);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // 401: oturumu yenile ve bir kez daha dene
        _logger.LogDebug("[SAP Auth] 401 Unauthorized — re-logging in for {DataSourceKey}", ctx.DataSourceKey);
        response.Dispose();

        var freshCookieHeader = await _sessionManager.ForceReLoginAsync(ctx, cancellationToken);
        var retryRequest = await CloneAsync(request, cancellationToken);
        SetCookie(retryRequest, freshCookieHeader);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static void SetCookie(HttpRequestMessage request, string cookieHeader)
    {
        request.Headers.Remove("Cookie");
        if (!string.IsNullOrEmpty(cookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage original,
        CancellationToken ct)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        foreach (var h in original.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

#pragma warning disable CS0618
        foreach (var p in original.Properties)
            clone.Properties[p.Key] = p.Value;
#pragma warning restore CS0618

        if (original.Content is not null)
        {
#pragma warning disable CA2016 // ReadAsByteArrayAsync(CancellationToken) yok netstandard2.0'da
            var bytes = await original.Content.ReadAsByteArrayAsync();
#pragma warning restore CA2016
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        return clone;
    }
}
