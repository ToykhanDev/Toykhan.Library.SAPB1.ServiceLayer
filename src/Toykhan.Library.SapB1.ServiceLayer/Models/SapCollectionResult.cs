using System.Text.Json.Serialization;

namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer OData koleksiyon yanıtını temsil eder.
/// <para>
/// <c>value</c> dizisini, sayfalama bağlantısını ve opsiyonel toplam kayıt sayısını içerir.
/// OData v1 (<c>odata.*</c>) ve v2 (<c>@odata.*</c>) yanıt formatlarının ikisi de desteklenir.
/// </para>
/// </summary>
/// <typeparam name="T">Koleksiyon eleman tipi.</typeparam>
public sealed class SapCollectionResult<T>
{
    /// <summary>Bu sayfadaki kayıtlar.</summary>
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();

    /// <summary>OData v1 biçimli sonraki sayfa bağlantısı.</summary>
    [JsonPropertyName("odata.nextLink")]
    public string? ODataNextLink { get; set; }

    /// <summary>OData v2 biçimli sonraki sayfa bağlantısı.</summary>
    [JsonPropertyName("@odata.nextLink")]
    public string? AtODataNextLink { get; set; }

    /// <summary>OData v1 biçimli toplam kayıt sayısı ($inlinecount=allpages).</summary>
    [JsonPropertyName("odata.count")]
    public int? ODataCount { get; set; }

    /// <summary>OData v2 biçimli toplam kayıt sayısı ($inlinecount=allpages).</summary>
    [JsonPropertyName("@odata.count")]
    public int? AtODataCount { get; set; }

    /// <summary>Sonraki sayfa bağlantısı (v1 ve v2 arasında uyumlu).</summary>
    [JsonIgnore]
    public string? NextLink => ODataNextLink ?? AtODataNextLink;

    /// <summary>Toplam kayıt sayısı (v1 ve v2 arasında uyumlu). Yalnızca $inlinecount=allpages ile dolar.</summary>
    [JsonIgnore]
    public int? TotalCount => ODataCount ?? AtODataCount;

    /// <summary>Sonraki sayfa mevcut mu?</summary>
    [JsonIgnore]
    public bool HasNextPage => !string.IsNullOrEmpty(NextLink);
}
