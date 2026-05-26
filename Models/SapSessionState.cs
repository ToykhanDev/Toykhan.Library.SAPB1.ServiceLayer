using System;
using System.Text.Json.Serialization;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// Distributed cache'te tutulan SAP B1 oturum durumu.
/// <para>System.Text.Json ile JSON olarak serileştirilir.</para>
/// </summary>
public sealed class SapSessionState
{
    /// <summary>SAP tarafından dönen oturum kimliği.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>SAP Business One sunucu versiyonu.</summary>
    public string SboVersion { get; set; } = string.Empty;

    /// <summary>Oturum ömrü (dakika).</summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>Login işleminin gerçekleştiği zaman (UTC).</summary>
    public DateTime LoginAtUtc { get; set; }

    /// <summary>
    /// Oturum çerez değerleri tek satırda. Format: <c>B1SESSION=xxx; CompanyDB=yyy; ROUTEID=zzz</c>
    /// <br/>Her HTTP isteğinde <c>Cookie</c> header'ına yazılır.
    /// </summary>
    public string CookieHeader { get; set; } = string.Empty;
}
