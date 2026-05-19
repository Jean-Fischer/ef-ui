# JSON-Backed Table Data Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Replace reload-driven Tabulator list interactions with a JSON-backed data flow that refreshes rows, status, and URL state without full-page blinking.

**Architecture:** Keep the list page server-rendered, add a per-entity JSON data endpoint that reuses the current query pipeline, and update the Tabulator enhancement to fetch JSON and mutate only the grid/status area. Preserve the current URL contract, server-authoritative semantics, and fallback HTML table.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core metadata/query pipeline, server-rendered HTML, Tabulator, xUnit, FluentAssertions.

---

### Task 1: Add a shared JSON list payload from the existing server query pipeline

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`

**Step 1: Write the failing tests**
- Add endpoint tests asserting `GET /simple/users/data` and `GET /chinook/tracks/data?...` return JSON with:
  - `listUrl`
  - `dataUrl`
  - `columns`
  - `rows`
  - `query`
  - `status`
- Add renderer test asserting list-page enhancement config now includes `dataUrl` and initial `status` metadata.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_data_endpoint|Get_tracks_data_endpoint|Render_list_emits_data_url"
```

Expected: FAIL because the `/data` route and config fields do not exist yet.

**Step 3: Write minimal implementation**
- In `EfUiApplicationBuilderExtensions`, add:
  - `GET {routePrefix}/{entity}/data`
- Factor the existing list pipeline into a shared helper that returns the authoritative list view and related lookups once.
- Return JSON from the new endpoint using the same rendered rows/query/status semantics as the HTML page.
- In `HtmlPageRenderer`, enrich the enhancement config to include:
  - `dataUrl`
  - initial `status` payload needed for client-side updates.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_data_endpoint|Get_tracks_data_endpoint|Get_entity_page_renders_active_query_state"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: add json list data payload"
```

---

### Task 2: Switch the Tabulator enhancement from full-page reloads to JSON refreshes

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiTableAssets.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`

**Step 1: Write the failing tests**
- Extend the asset test to assert the script now contains:
  - `fetch(`
  - `history.replaceState(`
  - `popstate`
  - the new `dataUrl`
- Assert the script no longer contains `window.location.assign(` for table interactions.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Get_table_enhancement_assets_expose_tabulator_bootstrap_shell
```

Expected: FAIL because the current script still navigates the whole page.

**Step 3: Write minimal implementation**
- Replace `navigate(...)` reload logic with a small request pipeline that:
  - reads current Tabulator sort/header-filter state
  - builds the existing query contract
  - updates `window.history.replaceState(...)`
  - fetches `dataUrl`
  - updates table rows in place
  - updates loading state and error/status area
- Add a guard so programmatic rehydration does not recursively trigger new requests.
- Add `popstate` handling to refetch and rehydrate from the URL.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Get_table_enhancement_assets_expose_tabulator_bootstrap_shell
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiTableAssets.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "feat: fetch table data without full page reloads"
```

---

### Task 3: Keep status, related-row prefilters, and operator semantics in sync

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiTableAssets.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`

**Step 1: Write the failing tests**
- Add/extend tests covering:
  - related-row prefilters returned by `/data`
  - `eq` semantics preserved for editable FK prefilters
  - JSON status payload includes visible active filter/sort/error strings
  - list-page config exposes enough status metadata for initial boot.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "data_endpoint|table_enhancement_assets|query_state"
```

Expected: FAIL until the status/operator data is fully synchronized.

**Step 3: Write minimal implementation**
- Ensure the JSON payload returns authoritative active filters/sorts/errors.
- Ensure columns continue to advertise the correct effective filter operator for editable related-row filters.
- Ensure the client can redraw the compact status strip from the JSON payload after each fetch.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_tracks|Get_invoice_items|Get_entity_page_renders_active_query_state"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiTableAssets.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "fix: sync json table status and filter semantics"
```

---

### Task 4: Update documentation and final verification

**Files:**
- Modify: `README.md`
- Verify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing doc/test expectations**
- Update README expectations for:
  - JSON-backed enhanced tables
  - no-blink refreshes
  - server-rendered fallback remaining available
  - URL synchronization without full reloads
- Add any last regression assertions if gaps remain.

**Step 2: Run focused verification**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: PASS.

**Step 3: Run full verification**

Run:
```bash
dotnet test EfUi.sln
```

Expected: PASS.

**Step 4: Manual smoke test**
- Run the sample host.
- Verify:
  - editing a header filter updates rows without full-page blink
  - the address bar updates
  - breadcrumbs remain stable
  - related-row prefilters are editable and stay visible
  - sorting still works without reload
  - fallback table still exists in the page source.

**Step 5: Commit**

```bash
git add README.md tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "docs: describe json-backed table refresh flow"
```
