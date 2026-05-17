# EF UI

## Run

```bash
dotnet run --project src/EfUi.SampleHost
```

Open `http://localhost:5000/efui` or the HTTPS URL shown by ASP.NET Core.

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
