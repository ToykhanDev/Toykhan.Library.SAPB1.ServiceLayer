using System.Text.Json.Serialization;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>SAP B1 Service Layer POST /Login yanıtı.</summary>
public sealed class SapLoginResponse
{
    /// <summary>Aktif oturum kimliği.</summary>
    [JsonPropertyName("SessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>SAP Business One sunucu versiyonu.</summary>
    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Oturum zaman aşımı (dakika). Sunucu yapılandırmasına göre değişir, varsayılan 30.</summary>
    [JsonPropertyName("SessionTimeout")]
    public int SessionTimeout { get; set; } = 30;
}
