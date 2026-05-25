using System.Threading;
using System.Threading.Tasks;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer oturum yaşam döngüsünü yönetir.
/// <para>
/// Thread-safe: aynı datasource için eşzamanlı login fırtınasını (login storm) engeller.
/// Her benzersiz <see cref="SapConnectionContext.SessionCacheKey"/> için yalnızca
/// bir login isteği eşzamanlı olarak gerçekleştirilebilir.
/// </para>
/// </summary>
public interface ISapSessionManager
{
    /// <summary>
    /// Geçerli oturum cookie header değerini döner.
    /// <para>Cache'te geçerli bir oturum varsa ömrü yenilenir ve döndürülür.
    /// Cache boşsa veya süresi dolmuşsa otomatik olarak login yapılır.</para>
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>
    /// <c>Cookie</c> header değeri. Örn: <c>B1SESSION=xxx; CompanyDB=yyy; ROUTEID=zzz</c>
    /// </returns>
    Task<string> GetOrRefreshAsync(SapConnectionContext ctx, CancellationToken ct = default);

    /// <summary>
    /// Önbellekteki oturumu geçersiz kılarak yeniden login yapar.
    /// <para>401 Unauthorized yanıtı alındığında çağrılmalıdır.</para>
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Yeni oturum cookie header değeri.</returns>
    Task<string> ForceReLoginAsync(SapConnectionContext ctx, CancellationToken ct = default);

    /// <summary>
    /// Önbellekteki oturum bilgisini siler. Service Layer'a Logout isteği göndermez.
    /// <para>Sonraki istek otomatik olarak yeniden login yapar.</para>
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task InvalidateAsync(SapConnectionContext ctx, CancellationToken ct = default);

    /// <summary>
    /// SAP Service Layer'a <c>POST /Logout</c> gönderir ve önbellekteki oturumu temizler.
    /// <para>Uygulama kapatılırken veya kullanıcı oturumu bilinçli sonlandırırken çağrılmalıdır.</para>
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task LogoutAsync(SapConnectionContext ctx, CancellationToken ct = default);
}
