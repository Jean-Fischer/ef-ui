# EF UI

> Give your existing EF Core app a built-in CRUD UI.

EF UI adds a server-rendered admin experience to an existing ASP.NET Core app with a `DbContext` already registered in dependency injection. It works with any EF Core provider and is packaged as `EfUi.AspNetCore`.

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

## Try it locally

```bash
dotnet run --project src/EfUi.SampleHost
```

Open `http://localhost:5000/` or the URL shown by ASP.NET Core.

## Docs

- Package details: [src/EfUi.AspNetCore/README.md](src/EfUi.AspNetCore/README.md)
- Release and publishing: `docs/publishing.md`

## Browser tests

Before the first run in a fresh checkout, trust the mise config once:

```bash
mise trust
```

Then install the Playwright browser binary and run the browser coverage:

```bash
mise run playwright-install
mise run test-browser
```

## License

MIT — see `LICENSE`.
