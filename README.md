# EF UI

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
- supported many-to-many skip navigations render as a filterable checkbox picker with client-side contains search
- supported one-to-many relationships render on edit forms as a filterable checkbox picker, with rows already assigned elsewhere shown disabled
- join entities with payload are managed through related-row links instead of inline nested editors

List behavior:
- list pages render a visible query-builder bar above the table
- list state is URL-driven using `filter.N.field`, `filter.N.op`, `filter.N.value`, `sort.N.field`, `sort.N.dir`, `offset`, and `limit`
- filtering and sorting are executed on the server
- FK display cells can render as links to the related row edit page
- related-row links can open child tables with a visible pre-applied filter in the URL
- list pages include a progressive Tabulator enhancement shell, while keeping the server-rendered HTML table as fallback

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

Artifacts are written to `%LOCALAPPDATA%\pi\ef-ui\sonar\`:
- `summary.md`
- `summary.json`
