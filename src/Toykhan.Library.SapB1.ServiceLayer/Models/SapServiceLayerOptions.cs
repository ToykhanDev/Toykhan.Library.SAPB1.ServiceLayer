namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer bağlantı davranışını yapılandırır.
/// <code>services.AddSapB1ServiceLayer(o => { o.RetryCount = 5; })</code> ile ayarlanır.
/// </summary>
public sealed class SapServiceLayerOptions
{
    /// <summary>Geçici hata (500/502/503/504) sonrası yeniden deneme sayısı. Varsayılan: 3.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// İlk yeniden deneme bekleme süresi (ms). Her denemede iki katına çıkar.
    /// Varsayılan: 200 ms.
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>İstek zaman aşımı saniyesi. Varsayılan: 100.</summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Self-signed SAP sunucu sertifikasını kabul et. Varsayılan: false.
    /// <para>Yalnızca geliştirme/test ortamında <c>true</c> yapılabilir. Üretimde kesinlikle <c>false</c> olmalı.</para>
    /// </summary>
    public bool SkipCertificateValidation { get; set; } = false;

    /// <summary>
    /// Oturum yanıtında <c>SessionTimeout</c> değeri gelmediğinde kullanılacak
    /// varsayılan oturum ömrü (dakika). Varsayılan: 30.
    /// </summary>
    public int DefaultSessionTimeoutMinutes { get; set; } = 30;
}
