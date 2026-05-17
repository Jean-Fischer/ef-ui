# Enable EF UI In Production Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Make the sample host serve EF UI in production as well as development.

**Architecture:** Keep the library-level production safeguard intact, but opt the sample host into production usage explicitly by setting `EnableInProduction = true`. Add tests that prove the sample host serves `/efui` in production while the library still allows consumers to disable production exposure.

**Tech Stack:** ASP.NET Core minimal hosting, xUnit, WebApplicationFactory, EF Core, C#

---

### Task 1: Add a failing production-host test for the sample app

**Files:**
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs`
- Create: `tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs`

**Step 1: Write the failing test**

Create a production-flavored factory and a test that requests `/efui` and expects `200 OK` with EF UI content.

```csharp
[Fact]
public async Task Sample_host_serves_efui_in_production()
{
    using var factory = new ProductionEfUiApplicationFactory();
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/efui");
    var html = await response.Content.ReadAsStringAsync();

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    html.Should().Contain("EF UI");
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Sample_host_serves_efui_in_production`

Expected: FAIL because production currently returns `404 Not Found`.

**Step 3: Add the minimal factory support**

Extend `EfUiApplicationFactory.cs` with a second factory that uses `builder.UseEnvironment("Production")`.

**Step 4: Run test to verify it still fails for the right reason**

Run the same command.

Expected: FAIL with `404` until the sample host is changed.

**Step 5: Commit checkpoint**

```bash
git add tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs
git commit -m "test: cover sample host EF UI in production"
```

### Task 2: Opt the sample host into production exposure

**Files:**
- Modify: `src/EfUi.SampleHost/Program.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs`

**Step 1: Write the minimal implementation**

Change the sample host configuration to explicitly allow production:

```csharp
app.UseEfUi(options =>
{
    options.DbContextType = typeof(SampleDbContext);
    options.RoutePrefix = "/efui";
    options.EnableInProduction = true;
});
```

**Step 2: Run the focused production test**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Sample_host_serves_efui_in_production`

Expected: PASS.

**Step 3: Commit checkpoint**

```bash
git add src/EfUi.SampleHost/Program.cs tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs
git commit -m "feat: enable sample host EF UI in production"
```

### Task 3: Verify existing behavior stays healthy

**Files:**
- Verify only: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Verify only: `tests/EfUi.AspNetCore.Tests/EscapedStringKeyRoutingTests.cs`

**Step 1: Run the full AspNetCore test project**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`

Expected: PASS.

**Step 2: Run the full solution test suite**

Run: `dotnet test EfUi.sln`

Expected: PASS.

**Step 3: Manual verification**

Run the sample host in production:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5055"
dotnet run --project src/EfUi.SampleHost --no-launch-profile
```

Open `http://127.0.0.1:5055/efui`.

Expected: EF UI index page renders instead of 404.

**Step 4: Commit checkpoint**

```bash
git add src/EfUi.SampleHost/Program.cs tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs
git commit -m "test: verify production sample host behavior"
```

### Task 4: Update developer guidance

**Files:**
- Modify: `README.md`

**Step 1: Document production run example**

Add a short section showing how to launch the sample host with `ASPNETCORE_ENVIRONMENT=Production` and confirm `/efui` stays available.

**Step 2: Run a quick README sanity check**

Ensure the commands reference the correct project path and URL.

**Step 3: Commit checkpoint**

```bash
git add README.md
git commit -m "docs: document production sample host usage"
```
