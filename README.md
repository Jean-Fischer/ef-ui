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
- supported many-to-many skip navigations render as multi-selects

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
