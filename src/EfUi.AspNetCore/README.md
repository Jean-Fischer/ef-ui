# EfUi.AspNetCore

> Add a built-in CRUD UI to your existing EF Core app.

`EfUi.AspNetCore` adds EF UI to an ASP.NET Core app that already has a `DbContext` registered in dependency injection. It works with .NET 8+ and any EF Core provider.

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
