using System;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer ham HTTP hata yanıt gövdesini <see cref="SapServiceLayerException"/>'a dönüştürür.
/// <para>
/// SAP B1, farklı sürümlerde ve senaryolarda farklı hata JSON formatları kullanır.
/// Bu arayüzün implementasyonu her iki formatı normalize eder.
/// </para>
/// </summary>
public interface ISapErrorTranslator
{
    /// <summary>
    /// Ham HTTP yanıt gövdesini ayrıştırıp <see cref="SapServiceLayerException"/> üretir.
    /// <para>
    /// JSON ayrıştırması başarısız olursa genel bir hata mesajı içeren exception döner;
    /// asla <c>null</c> veya başka bir istisna türü fırlatılmaz.
    /// </para>
    /// </summary>
    /// <param name="responseBody">SAP Service Layer'ın döndürdüğü ham JSON string.</param>
    /// <param name="cause">Asıl HTTP istisnası, varsa <see cref="Exception.InnerException"/> olarak atanır.</param>
    /// <returns>Normalize edilmiş SAP hata istisnası.</returns>
    SapServiceLayerException Translate(string responseBody, Exception? cause = null);
}
