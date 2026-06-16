# Project Instructions

## What this repo is
- EF UI is a server-rendered CRUD UI for existing ASP.NET Core apps with an EF Core `DbContext` already registered.
- Primary package: `EfUi.AspNetCore`.
- Supporting library: `EfUi.Core`.
- Sample host: `src/EfUi.SampleHost`.
- Test projects: `tests/EfUi.Core.Tests` and `tests/EfUi.AspNetCore.Tests`.
- The solution file is `EfUi.sln`.

## Setup / install
- The repo targets `.NET 8`, `.NET 9`, and `.NET 10`.
- `Directory.Build.props` enables nullable reference types, implicit usings, and `LangVersion=latest`.
- Repo task orchestration is handled with `mise` (`mise.toml`).
- On a fresh checkout, trust the mise config once: `mise trust`.
- When you need an explicit restore, use: `dotnet restore EfUi.sln`.

## Common commands
- Build the solution: `dotnet build EfUi.sln -c Release`
- Run all tests: `dotnet test EfUi.sln -c Release --no-build`
- Run the sample host: `dotnet run --project src/EfUi.SampleHost --framework net8.0`
- Install Playwright browsers: `mise run playwright-install`
- Run browser coverage: `mise run test-browser`
- Run the SonarCloud scan: `mise run sonar`
- Run the Sonar script directly: `pwsh -File scripts/sonar-scan.ps1`
- Publish the NuGet package: `mise run release-nuget`
- The publish task expects `PACKAGE_VERSION` and `NUGET_API_KEY`.

## Testing and verification
- CI builds and tests both projects against `net8.0`, `net9.0`, and `net10.0`.
- Prefer `Release` for parity with CI unless you are debugging.
- Browser coverage depends on Playwright being installed first.
- The browser suite currently focuses on `ChinookPlaywrightTests`.
- Use the matching `mise run ...` task when verifying release or Sonar changes.
- If a change affects runtime behavior, verify both the library project and the sample host path when practical.

## Code / file conventions
- Keep changes compatible with all three target frameworks.
- Avoid introducing APIs that only exist on one target unless you guard them carefully.
- Follow the existing namespace layout mirrored by the folder structure under `src/` and `tests/`.
- Keep files small and focused; the repo already separates core logic, ASP.NET Core integration, sample host code, and tests.
- `src/EfUi.AspNetCore/README.md` is packaged into the NuGet; keep package-facing behavior documented there.
- The sample host uses SQLite-backed databases and seeds data on startup.
- When adding display-label behavior, the package already exposes `EfUi.Core.Metadata.EfUiDisplayColumnAttribute`.

## Safety / avoid rules
- Do not commit build outputs: `bin/`, `obj/`, `.artifacts/`, `.sonarqube/`, or `.worktrees/`.
- Do not commit local sample databases: `src/EfUi.SampleHost/sample.db*` and `src/EfUi.SampleHost/edge-cases.db*`.
- Keep Sonar scan artifacts outside the repo; the script writes to `%LOCALAPPDATA%\pi\ef-ui\sonar\`.
- Avoid hard-coding machine-specific absolute paths into source or docs.
- The current package notes list these limitations: single-column primary keys only; composite primary keys and composite foreign keys are not supported.
- Keep release-flow changes aligned with `docs/publishing.md` and `.github/workflows/publish-nuget.yml`.
- Do not create `CLAUDE.md` unless the user explicitly asks for Claude Code compatibility.

## Notes for working in this repo
- Canonical repo docs: `README.md`, `src/EfUi.AspNetCore/README.md`, `docs/publishing.md`, `mise.toml`, and `.github/workflows/*.yml`.
- The root README is the quickest place to confirm install, browser-test, and Sonar commands.
- The sample host routes include `/simple`, `/edge-cases`, and `/chinook`.
- The package README describes `UseEfUi`, common options, and current limitations; prefer it over guessing behavior.
- There was no pre-existing root `AGENTS.md`, `CLAUDE.md`, or `.pi/SYSTEM.md` when this file was written.
