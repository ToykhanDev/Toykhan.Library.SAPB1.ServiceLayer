using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Toykhan.Library.SapB1.ServiceLayer.Internal;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer servislerini <see cref="IServiceCollection"/>'a ekleyen extension metotları.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// SAP B1 Service Layer servislerini kayıt eder.
    /// <para>
    /// Distributed cache daha önce kayıt edilmemişse in-memory fallback kullanılır.
    /// Üretim ortamında Redis gibi paylaşımlı bir cache kullanılması önerilir.
    /// </para>
    /// </summary>
    /// <param name="services">Servis koleksiyonu.</param>
    /// <param name="configure">Seçenekleri özelleştirme aksiyonu (opsiyonel).</param>
    public static IServiceCollection AddSapB1ServiceLayer(
        this IServiceCollection services,
        Action<SapServiceLayerOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure<SapServiceLayerOptions>(configure);

        // Distributed cache kayıtlı değilse in-memory fallback.
        // Üretimde Redis gibi paylaşımlı bir cache AddStackExchangeRedisCache ile eklenmeli.
        services.AddDistributedMemoryCache();

        // İç servisler
        services.AddSingleton<ISapSessionManager, SapSessionManager>();
        services.AddSingleton<ISapServiceLayerClient, SapServiceLayerClient>();
        services.AddTransient<ISapErrorTranslator, SapErrorTranslator>();

        // Named HttpClient kaydı (gerçek seçenekler DI'dan okunur)
        RegisterHttpClient(services);

        return services;
    }

    /// <summary>
    /// SAP B1 Service Layer servislerini <see cref="IConfiguration"/> bölümünden yapılandırarak kayıt eder.
    /// </summary>
    /// <param name="services">Servis koleksiyonu.</param>
    /// <param name="configuration">Uygulama konfigürasyonu.</param>
    /// <param name="sectionName">Yapılandırma bölüm adı. Varsayılan: <c>SapB1ServiceLayer</c></param>
    public static IServiceCollection AddSapB1ServiceLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "SapB1ServiceLayer")
    {
        services.Configure<SapServiceLayerOptions>(configuration.GetSection(sectionName));
        return services.AddSapB1ServiceLayer();
    }

    // ─── Private ───────────────────────────────────────────────────────────────

    private static void RegisterHttpClient(IServiceCollection services)
    {
        services
            .AddHttpClient("sapb1")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var optionsAccessor = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SapServiceLayerOptions>>();
                var opts = optionsAccessor.Value;

                return new HttpClientHandler
                {
                    UseCookies = false, // Cookie'leri SapAuthDelegatingHandler yönetir
                    ServerCertificateCustomValidationCallback = opts.SkipCertificateValidation
                        ? (_, _, _, _) => true
                        : (Func<HttpRequestMessage, System.Security.Cryptography.X509Certificates.X509Certificate2?, System.Security.Cryptography.X509Certificates.X509Chain?, System.Net.Security.SslPolicyErrors, bool>?)null
                };
            })
            .AddHttpMessageHandler(sp =>
            {
                var sessionManager = sp.GetRequiredService<ISapSessionManager>();
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SapAuthDelegatingHandler>();
                return new SapAuthDelegatingHandler(sessionManager, logger);
            })
            .AddStandardResilienceHandler(r =>
            {
                // Zaman aşımı ve retry ayarları servis başladıktan sonra IOptions'dan dinamik okunamaz,
                // bu yüzden makul sabit varsayılanlar kullanılır.
                // İnce ayar için AddSapB1ServiceLayer(config, section) overload'ı ile yapılandırın.
                r.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(600);

                r.Retry.MaxRetryAttempts = 3;
                r.Retry.Delay = TimeSpan.FromMilliseconds(200);
                r.Retry.UseJitter = true;

                // Yalnızca transient sunucu hatalarını yeniden dene (401 SapAuthDelegatingHandler'da işleniyor)
                r.Retry.ShouldHandle = args =>
                {
                    if (args.Outcome.Exception is not null)
                        return new System.Threading.Tasks.ValueTask<bool>(true);

                    var status = args.Outcome.Result?.StatusCode;
                    var retry =
                        status == System.Net.HttpStatusCode.InternalServerError ||
                        status == System.Net.HttpStatusCode.BadGateway ||
                        status == System.Net.HttpStatusCode.ServiceUnavailable ||
                        status == System.Net.HttpStatusCode.GatewayTimeout;

                    return new System.Threading.Tasks.ValueTask<bool>(retry);
                };

                // Circuit breaker SAP isteklerinde istemiyoruz — devre dışı
                r.CircuitBreaker.ShouldHandle = _ => new System.Threading.Tasks.ValueTask<bool>(false);
            });
    }
}
