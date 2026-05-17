# Chinook Multi-Context Sample Host Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Expose two EF UI mounts from the sample host: `/simple` backed by `SampleDbContext` and `/chinook` backed by a scaffolded `ChinookDbContext` generated from `db/chinook.db`.

**Architecture:** Keep the implementation explicit in `src/EfUi.SampleHost`. Register two `DbContext` types, seed only the sample database, scaffold Chinook into a dedicated folder/namespace, replace the root redirect with a tiny landing page, and mount EF UI twice with different route prefixes. Verify both mounts in development and production, plus at least one Chinook update flow.

**Tech Stack:** ASP.NET Core minimal hosting, EF Core 8, SQLite, `dotnet ef`, xUnit, WebApplicationFactory

---

### Task 1: Add failing tests for the new routing shape

**Files:**
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs` (only if extra factory helpers are needed)

**Step 1: Write the failing root-page test**

Add a test that requests `/` and verifies it returns `200 OK` with links to both mounts.

```csharp
[Fact]
public async Task Get_root_returns_links_to_all_ui_mounts()
{
    var response = await _client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    html.Should().Contain("/simple");
    html.Should().Contain("/chinook");
}
```

**Step 2: Update existing sample-route tests to the new prefix**

Change existing endpoint tests from `/efui/...` to `/simple/...`.

Examples:

```csharp
var html = await _client.GetStringAsync("/simple");
html.Should().Contain("/simple/users");
```

**Step 3: Add a failing production test for both mounts**

Update `EfUiProductionTests.cs` so production verifies:

```csharp
var simpleResponse = await client.GetAsync("/simple");
var chinookResponse = await client.GetAsync("/chinook");

simpleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
chinookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
```

**Step 4: Run the focused tests to verify failure**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_root_returns_links_to_all_ui_mounts|Sample_host_serves_efui_in_production"
```

Expected: FAIL because the app still redirects `/` and does not yet expose `/simple` and `/chinook`.

**Step 5: Commit checkpoint**

```bash
git add tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs
git commit -m "test: cover multi-context sample host routes"
```

### Task 2: Scaffold the Chinook EF Core model

**Files:**
- Create: `src/EfUi.SampleHost/Chinook/ChinookDbContext.cs`
- Create: `src/EfUi.SampleHost/Chinook/*.cs`
- Verify: `src/EfUi.SampleHost/EfUi.SampleHost.csproj`

**Step 1: Verify `dotnet ef` is available**

Run:

```bash
dotnet ef --version
```

Expected: version prints successfully.

**Step 2: Scaffold the Chinook model**

Run from repo root:

```bash
dotnet ef dbcontext scaffold "Data Source=db/chinook.db" Microsoft.EntityFrameworkCore.Sqlite --project src/EfUi.SampleHost --startup-project src/EfUi.SampleHost --output-dir Chinook --context-dir Chinook --context ChinookDbContext --namespace EfUi.SampleHost.Chinook --force
```

Expected: scaffolded context and entities appear under `src/EfUi.SampleHost/Chinook/`.

**Step 3: Inspect the generated code for obvious issues**

Review at minimum:
- `src/EfUi.SampleHost/Chinook/ChinookDbContext.cs`
- a few representative entities with scalar primary keys

Confirm namespace, connection setup, and generated `OnModelCreating` compile cleanly.

**Step 4: Build the sample host project**

Run:

```bash
dotnet build src/EfUi.SampleHost/EfUi.SampleHost.csproj
```

Expected: PASS.

**Step 5: Commit checkpoint**

```bash
git add src/EfUi.SampleHost/Chinook src/EfUi.SampleHost/EfUi.SampleHost.csproj
git commit -m "feat: scaffold Chinook EF Core model"
```

### Task 3: Register both contexts and add the tiny landing page

**Files:**
- Modify: `src/EfUi.SampleHost/Program.cs`
- Modify: `src/EfUi.SampleHost/appsettings.json`
- Modify: `src/EfUi.SampleHost/Data/SampleDbSeeder.cs` (only if startup flow needs a clearer helper boundary)

**Step 1: Add a Chinook connection string**

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Sample": "Data Source=sample.db",
    "Chinook": "Data Source=db/chinook.db"
  }
}
```

**Step 2: Register the second `DbContext`**

Update `Program.cs` to add:

```csharp
builder.Services.AddDbContext<ChinookDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Chinook") ?? "Data Source=db/chinook.db"));
```

Keep the existing `SampleDbContext` registration.

**Step 3: Keep seeding only for the sample database**

Startup should still create/seed only `SampleDbContext`.

Do not run any Chinook seeding or `EnsureCreated` logic on the Chinook database.

**Step 4: Replace the root redirect with a tiny HTML landing page**

Replace:

```csharp
app.MapGet("/", () => Results.Redirect("/efui"));
```

with something intentionally small, e.g.:

```csharp
app.MapGet("/", () => Results.Content(
    "<html><body><h1>EF UI Samples</h1><ul><li><a href=\"/simple\">Simple</a></li><li><a href=\"/chinook\">Chinook</a></li></ul></body></html>",
    "text/html"));
```

**Step 5: Mount EF UI twice**

Add two explicit mounts:

```csharp
app.UseEfUi(options =>
{
    options.DbContextType = typeof(SampleDbContext);
    options.RoutePrefix = "/simple";
    options.EnableInProduction = true;
});

app.UseEfUi(options =>
{
    options.DbContextType = typeof(ChinookDbContext);
    options.RoutePrefix = "/chinook";
    options.EnableInProduction = true;
});
```

**Step 6: Run the focused route tests**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_root_returns_links_to_all_ui_mounts|Get_index_returns_entity_links|Sample_host_serves_efui_in_production"
```

Expected: sample mount and production route tests move to green. Chinook-specific assertions may still need follow-up depending on the scaffolded model.

**Step 7: Commit checkpoint**

```bash
git add src/EfUi.SampleHost/Program.cs src/EfUi.SampleHost/appsettings.json src/EfUi.SampleHost/Data/SampleDbSeeder.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs
git commit -m "feat: mount sample and Chinook UIs"
```

### Task 4: Add focused Chinook endpoint coverage

**Files:**
- Create or modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Possibly inspect: `src/EfUi.SampleHost/Chinook/*.cs`

**Step 1: Pick a simple editable Chinook entity**

Choose a scaffolded table with:
- scalar primary key
- mostly scalar editable properties
- low relationship complexity

Examples might include entities like `Genre`, `MediaType`, or another simple lookup-style table, depending on the scaffolded model.

**Step 2: Write a failing list-page test**

Example shape:

```csharp
[Fact]
public async Task Get_chinook_index_returns_entity_links()
{
    var html = await _client.GetStringAsync("/chinook");

    html.Should().Contain("/chinook/");
}
```

**Step 3: Write a failing update-flow test**

Use a real scaffolded entity and verify edit/update works through the Chinook mount.

Example shape:

```csharp
[Fact]
public async Task Post_update_chinook_entity_persists_changes()
{
    var response = await _client.PostAsync("/chinook/<entity>/<id>", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["Name"] = "Updated Name"
    }));

    response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);

    var html = await _client.GetStringAsync("/chinook/<entity>/<id>/edit");
    html.Should().Contain("Updated Name");
}
```

**Step 4: Run the focused Chinook tests to verify failure**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Chinook
```

Expected: FAIL until entity names/paths/assertions line up with the scaffolded model and host wiring.

**Step 5: Adjust tests to the actual scaffolded entity names and forms**

Use the generated entity names and route names that EF UI exposes after scaffolding. Keep the first CRUD test minimal.

**Step 6: Re-run the focused Chinook tests**

Run the same `--filter Chinook` command.

Expected: PASS.

**Step 7: Commit checkpoint**

```bash
git add tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "test: cover Chinook mount CRUD flow"
```

### Task 5: Verify the full application behavior

**Files:**
- Modify if needed: `README.md`
- Verify only: `src/EfUi.SampleHost/Program.cs`
- Verify only: `src/EfUi.SampleHost/appsettings.json`

**Step 1: Update README run instructions**

Document that the sample host now exposes:
- `/` for the tiny landing page
- `/simple` for the simple sample database
- `/chinook` for the Chinook database

Keep the instructions short.

**Step 2: Run the AspNetCore test project**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: PASS.

**Step 3: Run the full solution test suite**

Run:

```bash
dotnet test EfUi.sln
```

Expected: PASS.

**Step 4: Manually smoke test in development**

Run:

```bash
dotnet run --project src/EfUi.SampleHost --no-launch-profile
```

Verify manually:
- `/` shows two links
- `/simple` works
- `/chinook` works
- one Chinook update succeeds

**Step 5: Manually smoke test in production**

Run:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5055"
dotnet run --project src/EfUi.SampleHost --no-launch-profile
```

Verify manually:
- `http://127.0.0.1:5055/`
- `http://127.0.0.1:5055/simple`
- `http://127.0.0.1:5055/chinook`

**Step 6: Optional Sonar verification after implementation**

Run:

```bash
mise run sonar
```

Expected: PASS with no new high-impact findings.

**Step 7: Final commit**

```bash
git add README.md src/EfUi.SampleHost/Program.cs src/EfUi.SampleHost/appsettings.json src/EfUi.SampleHost/Chinook tests/EfUi.AspNetCore.Tests
git commit -m "feat: add Chinook multi-context sample host"
```
