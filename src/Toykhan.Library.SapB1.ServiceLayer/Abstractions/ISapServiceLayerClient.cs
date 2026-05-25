using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer üzerinde HTTP CRUD işlemleri gerçekleştirir.
/// <para>
/// Oturum yönetimi, hata çevirisi ve yeniden deneme mantığı bu arayüzün
/// arkasında gizlidir. Çağıran kod yalnızca iş verisiyle ilgilenir.
/// </para>
/// </summary>
public interface ISapServiceLayerClient
{
    /// <summary>
    /// Belirtilen kaynaktan koleksiyon çeker. OData <c>value</c> dizisini otomatik olarak açar.
    /// </summary>
    /// <typeparam name="T">Kayıt modeli.</typeparam>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı. Örn: <c>BusinessPartners</c></param>
    /// <param name="query">OData sorgu parametreleri (opsiyonel).</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<List<T>> GetCollectionAsync<T>(
        SapConnectionContext ctx,
        string resource,
        SapODataQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Belirtilen ID'ye sahip tek bir kaydı çeker.
    /// </summary>
    /// <typeparam name="T">Kayıt modeli.</typeparam>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı. Örn: <c>Orders</c></param>
    /// <param name="id">Kayıt anahtarı. String ise tek tırnak eklenir: <c>resource('id')</c>; sayısal ise parantezle: <c>resource(123)</c></param>
    /// <param name="query">OData sorgu parametreleri (opsiyonel).</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<T> GetSingleAsync<T>(
        SapConnectionContext ctx,
        string resource,
        object id,
        SapODataQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Koleksiyonu toplam kayıt sayısıyla birlikte çeker (<c>$inlinecount=allpages</c>).
    /// </summary>
    /// <typeparam name="T">Kayıt modeli.</typeparam>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı.</param>
    /// <param name="query">OData sorgu parametreleri.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Kayıtlar ve toplam sayı çifti.</returns>
    Task<(List<T> Items, int TotalCount)> GetWithInlineCountAsync<T>(
        SapConnectionContext ctx,
        string resource,
        SapODataQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Kaynaktaki tüm kayıtları otomatik sayfalama ile çeker.
    /// <para><b>Dikkat:</b> Büyük veri setlerinde bellek tüketimini göz önünde bulundurun.</para>
    /// </summary>
    /// <typeparam name="T">Kayıt modeli.</typeparam>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı.</param>
    /// <param name="query">OData sorgu parametreleri. <see cref="SapODataQuery.PageSize"/> ile sayfa boyutu ayarlanabilir.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<List<T>> GetAllPagesAsync<T>(
        SapConnectionContext ctx,
        string resource,
        SapODataQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Yeni bir kayıt oluşturur ve oluşturulan kaydı döner.
    /// </summary>
    /// <typeparam name="T">Dönen kayıt modeli.</typeparam>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı.</param>
    /// <param name="data">Oluşturulacak kayıt verisi. JSON serialize edilir.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task<T> PostAsync<T>(
        SapConnectionContext ctx,
        string resource,
        object data,
        CancellationToken ct = default);

    /// <summary>
    /// Yeni bir kayıt oluşturur. Yanıt gövdesi beklenmez (<c>Prefer: return-no-content</c>).
    /// <para>Geri dönüş değeri gerekmediğinde performans avantajı sağlar.</para>
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı.</param>
    /// <param name="data">Oluşturulacak kayıt verisi.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task PostNoContentAsync(
        SapConnectionContext ctx,
        string resource,
        object data,
        CancellationToken ct = default);

    /// <summary>
    /// Mevcut bir kaydı kısmen günceller (PATCH).
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı.</param>
    /// <param name="id">Güncellenecek kaydın anahtarı.</param>
    /// <param name="data">Güncellenecek alanlar ve değerleri.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task PatchAsync(
        SapConnectionContext ctx,
        string resource,
        object id,
        object data,
        CancellationToken ct = default);

    /// <summary>
    /// Belirtilen kaydı siler (DELETE).
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı.</param>
    /// <param name="resource">SAP Service Layer kaynak adı.</param>
    /// <param name="id">Silinecek kaydın anahtarı.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task DeleteAsync(
        SapConnectionContext ctx,
        string resource,
        object id,
        CancellationToken ct = default);

    /// <summary>
    /// SAP Service Layer'ın erişilebilirliğini test eder. Login gerektirmez.
    /// <para>SAP 9.3 PL10 ve üzeri sürümlerde desteklenir.</para>
    /// </summary>
    /// <param name="ctx">Bağlantı bağlamı (yalnızca <see cref="SapConnectionContext.BaseUrl"/> kullanılır).</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Ping başarılıysa <c>true</c>.</returns>
    Task<bool> PingAsync(
        SapConnectionContext ctx,
        CancellationToken ct = default);
}
