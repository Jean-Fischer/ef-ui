# EF UI

## NuGet package

The publishable package is `EfUi.AspNetCore`.

- targets .NET 8+
- validated against EF Core 8.x
- works with any EF Core provider
- the sample host in this repository uses SQLite, but the package itself is provider-agnostic

For package-specific setup and usage, see `src/EfUi.AspNetCore/README.md`.
For release workflow notes, see `docs/publishing.md`.

## Compatibility

EF UI targets .NET 8 and is intended to work with .NET 8+ host applications.
The current package baseline is EF Core 8.0.11.

## Run

```bash
dotnet run --project src/EfUi.SampleHost
```

Open `http://localhost:5000/` or the HTTPS URL shown by ASP.NET Core.

Available routes:
- `/` — tiny landing page
- `/simple` — sample database
- `/chinook` — Chinook database

Form behavior:
- database-generated primary keys are hidden on create and shown read-only on edit
- assigned primary keys stay editable on create and are shown read-only on edit
- many-to-one relationships render as dropdowns
- you can override FK display text with `EfUiDisplayColumn` on a navigation property, or set a class-level default on the related entity type
- supported many-to-many skip navigations render as a filterable checkbox picker with client-side contains search
- supported one-to-many relationships render on edit forms as a filterable checkbox picker, with rows already assigned elsewhere shown disabled
- join entities with payload are managed through related-row links instead of inline nested editors

Authorization:
- EF UI authorization is opt-in and uses standard ASP.NET Core role checks
- enable it with `options.RequireAuthorization = true`
- browsing routes accept users in either `ReadOnly` or `Edit`
- create, update, and delete routes require `Edit`
- the host app still owns authentication; EF UI only consumes the authenticated user and role claims
- unauthenticated requests return the framework's normal `401` response and forbidden requests return `403`
- the sample host includes a small dev-only auth switch on the home page so you can try anonymous, `ReadOnly`, and `Edit` profiles locally
- the Chinook mount is protected in the sample host so you can verify the authorization flow without wiring up a separate identity provider

Example:

```csharp
app.UseEfUi(options =>
{
    options.DbContextType = typeof(ChinookDbContext);
    options.RoutePrefix = "/chinook";
    options.RequireAuthorization = true;
});
```

List behavior:
- every page includes breadcrumb navigation so you can move back to EF UI home, the current mount, and the current entity list
- list state is URL-driven using `filter.N.field`, `filter.N.op`, `filter.N.value`, `sort.N.field`, `sort.N.dir`, `offset`, and `limit`
- Tabulator header sorting and header filters own the single visible table surface
- enhanced tables fetch JSON from `/<mount>/<entity>/data` and refresh rows in place instead of reloading the whole page
- the enhanced grid updates the address bar with the current query state without full-page blink
- the server owns filtering and sorting semantics and returns the authoritative result set for each query
- list pages show a compact table status strip for active filters, active sorts, and query validation errors
- FK display cells can render as links to the related row edit page using the configured display column
- related-row links can open child tables with a visible pre-applied filter in the URL
- list pages keep a server-rendered HTML table as fallback if the Tabulator enhancement is unavailable
- the enhanced grid uses in-grid loading while it refreshes table data

To verify the sample host in production mode:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5055"
dotnet run --project src/EfUi.SampleHost --no-launch-profile
```

Then open:
- `http://127.0.0.1:5055/`
- `http://127.0.0.1:5055/simple`
- `http://127.0.0.1:5055/chinook`

## Test

```bash
dotnet test EfUi.sln
```

## Sonar scan

At the end of each feature, run a Sonar scan and review the generated report before calling the work done.

From the repository root, with `dotnet`, the global `dotnet-sonarscanner` tool, and `EF_UI_SONAR_TOKEN` available in your shell, run either:

```bash
pwsh -NoProfile -File scripts/sonar-scan.ps1
```

or:

```bash
mise run sonar
```

If `mise` reports that `mise.toml` is not trusted yet, run `mise trust mise.toml` once from the repo root.

The script reports Sonar quality results and writes local artifacts so humans and Pi agents can inspect findings, even when the quality gate is red.

For feature completion, treat any new or introduced `HIGH`/`CRITICAL` impact finding or any `SECURITY` finding as a blocker until it is understood and resolved.

Artifacts are written to `%LOCALAPPDATA%\pi\ef-ui\sonar\`:
- `summary.md`
- `summary.json`
