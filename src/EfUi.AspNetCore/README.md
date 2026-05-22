# EfUi.AspNetCore

`EfUi.AspNetCore` is the NuGet package that adds EF UI to an ASP.NET Core application.

## What this package covers

- .NET 8+
- Entity Framework Core 8.x
- any EF Core database provider
- ASP.NET Core apps that can register a `DbContext` in dependency injection

The package is provider-agnostic. The sample host in this repository uses SQLite, but that is only one example.

## Install

```bash
dotnet add package EfUi.AspNetCore
```

## Use

```csharp
using EfUi.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MyDbContext>(...);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseEfUi(options =>
{
    options.DbContextType = typeof(MyDbContext);
    options.RoutePrefix = "/admin";
    options.EnableInProduction = true;
    options.RequireAuthorization = true;
});

app.Run();
```

## Options to know

- `DbContextType`: the EF Core `DbContext` type registered in DI
- `RoutePrefix`: the URL path where the UI is mounted
- `EnableInProduction`: keep the UI disabled in production unless you explicitly opt in
- `RequireAuthorization`: require authenticated users and role checks
- `ReadOnlyRoleName` / `EditRoleName`: override the default role names if your app uses different ones

## Notes

- The package exposes the `UseEfUi` ASP.NET Core extension method.
- When authorization is enabled, browsing routes accept `ReadOnly` or `Edit`, while create/update/delete routes require `Edit`.
- The package is intended for server-rendered admin-style EF Core browsing and editing.
