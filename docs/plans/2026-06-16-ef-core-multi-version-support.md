# EF Core Multi-Version Support Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Support EF Core 8 and EF Core 10 as first-class targets without forcing CRUD-only users onto the latest version, then finish with an explicit EF Core 9 / net9.0 compatibility pass so the final support matrix is a deliberate decision.

**Architecture:** Make the library and its host/test projects multi-targeted so each app TFM consumes the matching EF Core major version. Keep the runtime code on the smallest stable relational API surface (`DbContext`, `IEntityType`, `GetTableName`, `GetProperties`, `GetForeignKeys`, `GetNavigations`, `GetSkipNavigations`, `FindAsync`, `SaveChangesAsync`). Use CI to run the same smoke tests on net8.0 and net10.0 first, then add net9.0 at the end as a compatibility probe. Avoid version-specific branches unless a later EF10 API truly requires one.

**Tech Stack:** .NET 8 / 9 / 10, EF Core 8 / 9 / 10, `Microsoft.EntityFrameworkCore.Relational`, SQLite, ASP.NET Core minimal APIs, xUnit, FluentAssertions, GitHub Actions.

---

### Task 1: Make the core library relational-first and multi-targeted

**Files:**
- Modify: `Directory.Build.props`
- Modify: `src/EfUi.Core/EfUi.Core.csproj`
- Modify: `src/EfUi.AspNetCore/EfUi.AspNetCore.csproj`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Modify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Modify: `src/EfUi.AspNetCore/README.md`

**Step 1: Write the failing compatibility check**

Add or tighten the route-name test so it proves table names still drive EF UI routes. Then run a net10-only test invocation before the project supports it:

```bash
cd .worktrees/ef-core-compatibility-plan

dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net10.0 --no-restore
```

Expected: FAIL, because the repo currently only targets net8.0.

**Step 2: Write the minimal implementation**

Change the shared build target to `TargetFrameworks` and add conditional package references so the net8.0 build keeps EF Core 8 while the net10.0 build uses EF Core 10. Add `Microsoft.EntityFrameworkCore.Relational` alongside the core package in both library projects.

Replace the relational annotation lookup with the public API:

```csharp
var tableName = entityType.GetTableName();
return (tableName ?? entityType.ClrType.Name).ToLowerInvariant();
```

Keep the route-name fallback simple and deterministic.

**Step 3: Run the targeted tests again**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net10.0 --no-restore
```

Expected: PASS for both TFMs.

**Step 4: Commit**

```bash
git.exe add Directory.Build.props src/EfUi.Core/EfUi.Core.csproj src/EfUi.AspNetCore/EfUi.AspNetCore.csproj src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs src/EfUi.AspNetCore/README.md
git.exe commit -m "feat: multi-target ef ui for ef core 8 and 10"
```

---

### Task 2: Multi-target the sample host and ASP.NET Core tests

**Files:**
- Modify: `src/EfUi.SampleHost/EfUi.SampleHost.csproj`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`
- Modify: `tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/Browser/SampleHostProcess.cs`
- Modify: `.github/workflows/ci.yml`

**Step 1: Add the next compatibility smoke test**

Before changing the project files, run the ASP.NET Core integration tests against net10.0 to prove the current setup cannot exercise that target yet:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net10.0 --no-restore
```

Expected: FAIL, because the host/test stack is still built around the net8-only layout.

**Step 2: Write the minimal implementation**

Add `net10.0` to the sample host and both test projects so the test runner can actually consume the net10 build of the library.

Keep package versions aligned by TFM:
- EF Core SQLite provider for the core tests
- EF Core SQLite + `Microsoft.AspNetCore.Mvc.Testing` + `Microsoft.AspNetCore.TestHost` for the ASP.NET Core tests
- any other Microsoft packages that need to match the active target major

Update GitHub Actions to install .NET 8 and .NET 10 and run the same test suite twice, once per TFM. Keep the existing Playwright/browser coverage in place if it still passes cleanly under the selected target.

**Step 3: Run the project matrix**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net10.0 --no-restore
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net10.0 --no-restore
```

Expected: PASS for both TFMs on both test projects.

**Step 4: Commit**

```bash
git.exe add src/EfUi.SampleHost/EfUi.SampleHost.csproj tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs tests/EfUi.AspNetCore.Tests/Browser/SampleHostProcess.cs .github/workflows/ci.yml
git.exe commit -m "test: run ef ui against net8 and net10"
```

---

### Task 3: Update the docs and release notes to match the kept matrix

**Files:**
- Modify: `README.md`
- Modify: `src/EfUi.AspNetCore/README.md`
- Modify: `docs/publishing.md`

**Step 1: Write the documentation update after the matrix is green**

Update the supported-version wording so it matches the actual support decision after Tasks 1 and 2. Keep it plain and maintainable:
- name the TFMs you actually ship
- name the EF Core majors you actually validate
- mention that EF Core 9 is a separate compatibility check, not an automatic fallback

**Step 2: Verify the wording is accurate**

Run a quick check to make sure the new version claims are present in the docs and no stale "EF Core 8 only" phrasing remains:

```bash
grep -n "EF Core" README.md src/EfUi.AspNetCore/README.md docs/publishing.md
```

Then rerun the relevant targeted tests from Tasks 1 and 2 if any build files changed while updating the docs.

**Step 3: Commit**

```bash
git.exe add README.md src/EfUi.AspNetCore/README.md docs/publishing.md
git.exe commit -m "docs: describe ef core version support"
```

---

### Task 4: Add the EF Core 9 / net9.0 compatibility pass last

**Files:**
- Modify: `Directory.Build.props`
- Modify: `src/EfUi.Core/EfUi.Core.csproj`
- Modify: `src/EfUi.AspNetCore/EfUi.AspNetCore.csproj`
- Modify: `src/EfUi.SampleHost/EfUi.SampleHost.csproj`
- Modify: `tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`
- Modify: `.github/workflows/ci.yml`
- Modify: `docs/publishing.md`

**Step 1: Add the final failing net9.0 check**

Run the ASP.NET Core integration tests against net9.0 before the target exists:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net9.0 --no-restore
```

Expected: FAIL, because net9.0 is not wired up yet.

**Step 2: Write the minimal implementation**

Add `net9.0` to the target framework list everywhere that needs to participate in the matrix:
- the core library
- the ASP.NET Core library
- the sample host
- both test projects
- the CI workflow

Add the matching EF Core 9 / SQLite / TestHost package references so the net9 build actually runs against EF Core 9 instead of silently reusing another major.

**Step 3: Run the full three-way matrix**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net9.0 --no-restore
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release -f net10.0 --no-restore
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net9.0 --no-restore
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release -f net10.0 --no-restore
```

Expected: PASS if EF Core 9 is genuinely compatible with the same code surface.

If it passes, keep net9.0 and document it.

If it fails, remove net9.0 from the committed support matrix, but keep the notes about what failed and why so the next version bump is easier.

**Step 4: Commit the final support decision**

```bash
git.exe add Directory.Build.props src/EfUi.Core/EfUi.Core.csproj src/EfUi.AspNetCore/EfUi.AspNetCore.csproj src/EfUi.SampleHost/EfUi.SampleHost.csproj tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj .github/workflows/ci.yml docs/publishing.md
git.exe commit -m "feat: verify ef core 9 compatibility"
```
