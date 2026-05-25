using System;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer kaynaklı hataları temsil eder.
/// <para>
/// Standart <see cref="Exception"/> sınıfından türer; herhangi bir framework'e bağımlı değildir.
/// ABP, ASP.NET Core veya konsol uygulamalarında doğrudan kullanılabilir.
/// </para>
/// </summary>
public sealed class SapServiceLayerException : Exception
{
    /// <summary>
    /// SAP tarafından dönen hata kodu, string olarak normalize edilmiştir.
    /// <br/>Örnekler: <c>"100"</c>, <c>"-2028"</c>, <c>"401"</c>
    /// </summary>
    public string SapErrorCode { get; }

    /// <summary>
    /// Yeni bir <see cref="SapServiceLayerException"/> oluşturur.
    /// </summary>
    /// <param name="sapErrorCode">SAP hata kodu.</param>
    /// <param name="message">Kullanıcıya gösterilecek hata mesajı.</param>
    /// <param name="innerException">Asıl HTTP istisnası (opsiyonel).</param>
    public SapServiceLayerException(string sapErrorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        SapErrorCode = sapErrorCode;
    }

    /// <summary>
    /// Bu hata oturum geçersizliğini mi gösteriyor?
    /// <para><c>true</c> ise <see cref="ISapSessionManager.ForceReLoginAsync"/> çağrılmalıdır.</para>
    /// </summary>
    public bool IsUnauthorized =>
        SapErrorCode is "401" or "-2028" or "302";
}
