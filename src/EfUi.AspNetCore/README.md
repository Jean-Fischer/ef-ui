# EfUi.AspNetCore

> Add a built-in CRUD UI to your existing EF Core app.

`EfUi.AspNetCore` adds EF UI to an ASP.NET Core app that already has a `DbContext` registered in dependency injection. It works with .NET 8, .NET 9, and .NET 10, with matching EF Core 8, 9, and 10 support checks, and is built on EF Core relational APIs. The sample host in this repository uses SQLite.

## Install

```bash
dotnet add package EfUi.AspNetCore
```

## Quick start

```csharp
using EfUi.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MyDbContext>(...);

var app = builder.Build();

app.UseEfUi(options =>
{
    options.DbContextType = typeof(MyDbContext);
    options.RoutePrefix = "/admin";
});

app.Run();
```

## Common options

- `RoutePrefix` to mount the UI wherever you want
- `RequireAuthorization = true` to protect the UI with ASP.NET Core auth
- `EnableInProduction = true` to allow the UI outside Development
- `ReadOnlyRoleName` / `EditRoleName` if your app uses different role names
- `AntiforgeryKeyDirectory` to override where EF UI stores its Data Protection key ring for write-form tokens

## What you get

- CRUD pages over your EF Core entities
- relationship-aware forms and list pages
- server-rendered fallback with enhanced table browsing
- local, package-owned Tabulator assets for the enhanced list shell
- relational EF Core behavior for supported database providers

## Model annotations

`EfUi.Core.Metadata.EfUiDisplayColumnAttribute` lets you control which property EF UI uses as the display label for related rows and foreign-key dropdowns.

You can apply it to:

- a class, to set the default display property for that entity
- a navigation property, to override the display property for one relationship

A common pattern is to keep presentation logic in a partial class and expose a computed property:

```csharp
[EfUiDisplayColumn(nameof(FullName))]
public partial class Employee
{
    public string FullName => $"{FirstName} {LastName}";
}
```

If no attribute is present, EF UI falls back to `Name`, `Title`, `Email`, then the primary key.

## Notes

- The package exposes the `UseEfUi` ASP.NET Core extension method.
- The UI is designed for existing ASP.NET Core apps with a registered EF Core `DbContext`.
- The enhanced list shell self-hosts its Tabulator assets instead of fetching them from a CDN.
- Write forms and delete actions include hidden antiforgery tokens by default, and those tokens are paired with a route-scoped cookie.
- Entity routes stay table-driven; schema-qualified table names are prefixed with the schema when needed to keep routes unique.
- When authorization is enabled, browsing routes accept `ReadOnly` or `Edit`, while create, update, and delete routes require `Edit`.

## Current limitations

- Entities must have a single-column primary key.
- Composite primary keys are not supported yet.
- Composite foreign keys are not supported yet.
- The editor currently supports common scalar CLR types such as `string`, numeric types, `bool`, `DateTime`, `Guid`, and enums. The renderer uses type-specific controls for the supported scalar set: checkbox/select variants for booleans, number inputs for numeric types, a text input with ISO-8601 subset validation for `DateTime`, and text/select fallbacks for the remaining supported scalars.
- Very large tables are still rendered through in-memory row loading, so server-side query execution and pagination are not fully provider-driven yet.
