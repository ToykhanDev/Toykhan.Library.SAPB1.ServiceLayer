namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// Tek bir SAP B1 datasource'a bağlanmak için gereken bağlam.
/// <para>
/// <see cref="Password"/> alanı yalnızca bellekte yaşar; asla loglanmaz veya persist edilmez.
/// Çağıran tarafın şifreli depolamadan çözmesi ve bu record'a düz metin olarak vermesi beklenir.
/// </para>
/// </summary>
public sealed record SapConnectionContext
{
    /// <summary>
    /// Multi-tenant uygulamalarda tenant ayrımı için kullanılan opsiyonel anahtar.
    /// <c>null</c> geçerli bir değerdir (single-tenant senaryo).
    /// </summary>
    public string? TenantKey { get; init; }

    /// <summary>
    /// Bu datasource'u küresel olarak benzersiz tanımlayan anahtar.
    /// Örn: veritabanındaki ID'nin string karşılığı veya anlamlı bir takma ad.
    /// </summary>
    public required string DataSourceKey { get; init; }

    /// <summary>
    /// SAP B1 Service Layer kök adresi.
    /// Beklenen format: <c>https://[sunucu]:[port]/b1s/[versiyon]</c>
    /// <br/>Örn: <c>https://sapserver:50000/b1s/v2</c>
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>SAP şirket veritabanı (şema) adı.</summary>
    public required string CompanyDb { get; init; }

    /// <summary>SAP kullanıcı adı.</summary>
    public required string UserName { get; init; }

    /// <summary>
    /// SAP şifresi — düz metin, yalnızca istek süresince bellekte.
    /// Loglara, önbelleğe veya serileştirme çıktılarına asla yazılmamalıdır.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// SAP dil kodu (opsiyonel). Belirtilirse hata mesajları bu dilde döner.
    /// Kullanılabilir dil kodları için <c>GET UserLanguages</c> isteği yapılabilir.
    /// </summary>
    public int? Language { get; init; }

    /// <summary>Bu datasource için oluşturulacak named <see cref="System.Net.Http.HttpClient"/> adı.</summary>
    internal string HttpClientName => $"sapb1:{TenantKey}:{DataSourceKey}";

    /// <summary>Distributed cache'teki oturum girdisinin anahtarı.</summary>
    internal string SessionCacheKey =>
        $"sapb1:session:{TenantKey}:{DataSourceKey}:{CompanyDb}:{UserName}";
}
