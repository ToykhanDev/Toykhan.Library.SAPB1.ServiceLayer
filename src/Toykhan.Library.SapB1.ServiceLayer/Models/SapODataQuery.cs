namespace Toykhan.Library.SapB1.ServiceLayer;

/// <summary>
/// SAP B1 Service Layer OData sorgu parametrelerini oluşturmak için fluent builder.
/// </summary>
/// <example>
/// <code>
/// var query = SapODataQuery.New()
///     .WithFilter("DocDate gt '2025-01-01'")
///     .WithSelect("DocEntry,DocDate,DocTotal")
///     .WithOrderBy("DocEntry desc")
///     .WithTop(50)
///     .WithPageSize(50)
///     .AsCaseInsensitive();
/// </code>
/// </example>
public sealed class SapODataQuery
{
    /// <summary>$filter parametresi. Örn: <c>startswith(CardCode, 'C')</c></summary>
    public string? Filter { get; private set; }

    /// <summary>$select parametresi. Örn: <c>DocEntry,DocDate,DocTotal</c></summary>
    public string? Select { get; private set; }

    /// <summary>$orderby parametresi. Örn: <c>DocEntry desc</c></summary>
    public string? OrderBy { get; private set; }

    /// <summary>$top parametresi — döndürülecek maksimum kayıt sayısı.</summary>
    public int? Top { get; private set; }

    /// <summary>$skip parametresi — atlanacak kayıt sayısı (sayfalama).</summary>
    public int? Skip { get; private set; }

    /// <summary>$expand parametresi. Örn: <c>DocumentLines</c></summary>
    public string? Expand { get; private set; }

    /// <summary>$apply parametresi — gruplama/toplama. Örn: <c>groupby((CardCode), aggregate(DocTotal with sum as Total))</c></summary>
    public string? Apply { get; private set; }

    /// <summary>B1S-PageSize header değeri. SAP'ın sayfa başına döndürdüğü kayıt sayısını belirler.</summary>
    public int? PageSize { get; private set; }

    /// <summary>B1S-CaseInsensitive header'ı ekler. HANA veritabanlarında büyük/küçük harf duyarsız arama için.</summary>
    public bool CaseInsensitive { get; private set; }

    /// <summary>$inlinecount=allpages parametresini ekler. Toplam kayıt sayısını yanıta dahil eder.</summary>
    public bool InlineCount { get; private set; }

    /// <summary>Boş bir sorgu başlatır.</summary>
    public static SapODataQuery New() => new();

    /// <summary>$filter parametresini ayarlar.</summary>
    public SapODataQuery WithFilter(string filter) { Filter = filter; return this; }

    /// <summary>$select parametresini ayarlar.</summary>
    public SapODataQuery WithSelect(string select) { Select = select; return this; }

    /// <summary>$orderby parametresini ayarlar.</summary>
    public SapODataQuery WithOrderBy(string orderBy) { OrderBy = orderBy; return this; }

    /// <summary>$top parametresini ayarlar.</summary>
    public SapODataQuery WithTop(int top) { Top = top; return this; }

    /// <summary>$skip parametresini ayarlar.</summary>
    public SapODataQuery WithSkip(int skip) { Skip = skip; return this; }

    /// <summary>$expand parametresini ayarlar.</summary>
    public SapODataQuery WithExpand(string expand) { Expand = expand; return this; }

    /// <summary>$apply parametresini ayarlar.</summary>
    public SapODataQuery WithApply(string apply) { Apply = apply; return this; }

    /// <summary>B1S-PageSize header değerini ayarlar.</summary>
    public SapODataQuery WithPageSize(int size) { PageSize = size; return this; }

    /// <summary>B1S-CaseInsensitive header'ını etkinleştirir.</summary>
    public SapODataQuery AsCaseInsensitive() { CaseInsensitive = true; return this; }

    /// <summary>$inlinecount=allpages parametresini etkinleştirir.</summary>
    public SapODataQuery WithInlineCount() { InlineCount = true; return this; }
}
