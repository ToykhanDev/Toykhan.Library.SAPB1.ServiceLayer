# Toykhan.Library.SapB1.ServiceLayer

HTTP client helpers for SAP Business One Service Layer applications.

This package wraps common Service Layer concerns behind a small .NET API:

- Session login, refresh, logout, and distributed cache-backed session reuse
- OData query builder helpers for filter, select, order, paging, expand, apply, inline count, and SAP-specific headers
- Typed collection, single item, create, patch, delete, and ping operations
- Standard transient retry behavior for server-side failures
- Dependency injection setup for ASP.NET Core and worker services

## Install

From nuget.org:

```powershell
dotnet add package Toykhan.Library.SapB1.ServiceLayer --version 1.0.0
```

From GitHub Packages, add the GitHub Packages NuGet source first:

```powershell
dotnet nuget add source "https://nuget.pkg.github.com/ToykhanDev/index.json" `
  --name github `
  --username YOUR_GITHUB_USERNAME `
  --password YOUR_GITHUB_PAT `
  --store-password-in-clear-text
```

Use a GitHub personal access token (classic) with only the `read:packages` scope
for local installs. Then install the package:

```powershell
dotnet add package Toykhan.Library.SapB1.ServiceLayer --version 1.0.0 --source github
```

## Supported frameworks

- .NET Standard 2.0
- .NET 8.0
- .NET 9.0
- .NET 10.0

## Register services

```csharp
using Toykhan.Library.SapB1.ServiceLayer;

builder.Services.AddSapB1ServiceLayer(options =>
{
    options.SkipCertificateValidation = false;
    options.DefaultSessionTimeoutMinutes = 30;
});
```

For production, register a shared distributed cache such as Redis before calling
`AddSapB1ServiceLayer` so multiple application instances can reuse SAP sessions.
If no distributed cache is registered, the package uses an in-memory fallback.

## Query SAP Business One Service Layer

```csharp
using Toykhan.Library.SapB1.ServiceLayer;

public sealed class BusinessPartner
{
    public string? CardCode { get; set; }
    public string? CardName { get; set; }
}

public sealed class PartnerReader(ISapServiceLayerClient client)
{
    public Task<List<BusinessPartner>> GetCustomersAsync(CancellationToken ct)
    {
        var context = new SapConnectionContext
        {
            TenantKey = "default",
            DataSourceKey = "main-sap",
            BaseUrl = "https://sap-server.example.com:50000/b1s/v2",
            CompanyDb = "COMPANY_DB",
            UserName = "manager",
            Password = "load-from-secure-storage"
        };

        var query = SapODataQuery.New()
            .WithFilter("startswith(CardCode, 'C')")
            .WithSelect("CardCode,CardName")
            .WithOrderBy("CardCode asc")
            .WithTop(50)
            .WithPageSize(50);

        return client.GetCollectionAsync<BusinessPartner>(
            context,
            "BusinessPartners",
            query,
            ct);
    }
}
```

Do not hard-code production credentials. Load `Password` from a secure secret store
and pass it only for the duration of the SAP request.

## Configuration

You can bind options from configuration:

```csharp
builder.Services.AddSapB1ServiceLayer(
    builder.Configuration,
    sectionName: "SapB1ServiceLayer");
```

Example configuration:

```json
{
  "SapB1ServiceLayer": {
    "RetryCount": 3,
    "RetryBaseDelayMs": 200,
    "TimeoutSeconds": 100,
    "SkipCertificateValidation": false,
    "DefaultSessionTimeoutMinutes": 30
  }
}
```

`SkipCertificateValidation` should stay `false` in production. Use it only for
development or test SAP environments that rely on temporary certificates.

## License

This project is licensed under the MIT License.

## Trademark and affiliation notice

SAP and SAP Business One may be trademarks or registered trademarks of SAP SE or
its affiliates. This package is an independent open-source library and is not
affiliated with, endorsed by, sponsored by, or officially connected to SAP SE.
