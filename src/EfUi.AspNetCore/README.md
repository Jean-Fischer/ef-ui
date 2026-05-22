# EfUi.AspNetCore

> Add a built-in CRUD UI to your existing EF Core app.

`EfUi.AspNetCore` adds EF UI to an ASP.NET Core app that already has a `DbContext` registered in dependency injection. It works with .NET 8+, EF Core 8+, and is designed to be provider-agnostic. The sample host in this repository uses SQLite.

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

## What you get

- CRUD pages over your EF Core entities
- relationship-aware forms and list pages
- server-rendered fallback with enhanced table browsing
- provider-agnostic behavior across EF Core database providers

## Notes

- The package exposes the `UseEfUi` ASP.NET Core extension method.
- The UI is designed for existing ASP.NET Core apps with a registered EF Core `DbContext`.
- When authorization is enabled, browsing routes accept `ReadOnly` or `Edit`, while create, update, and delete routes require `Edit`.

## Current limitations

- Entities must have a single-column primary key.
- Composite primary keys are not supported yet.
- Composite foreign keys are not supported yet.
- The editor currently supports common scalar CLR types such as `string`, numeric types, `bool`, `DateTime`, `Guid`, and enums.
- Very large tables are still rendered through in-memory row loading, so server-side query execution and pagination are not fully provider-driven yet.
