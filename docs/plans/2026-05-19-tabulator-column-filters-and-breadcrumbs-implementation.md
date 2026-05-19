# Tabulator Column Filters and Breadcrumb Navigation Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Replace the top list query form with column-owned Tabulator sorting/filtering, add breadcrumb navigation across EF UI pages, and keep a clear table-local loading experience while preserving server-owned list semantics.

**Architecture:** Keep ASP.NET list endpoints authoritative for query binding, filtering, sorting, and rendered rows. Move the visible interaction surface into Tabulator by enriching the server-rendered table payload with column capability/state metadata, then use Tabulator header sorting/header filters plus a compact server-rendered status strip instead of the current top form. Add a shared breadcrumb renderer so index, list, and edit/create pages all expose lightweight navigation back to mount and entity pages.

**Tech Stack:** ASP.NET Core minimal APIs, EF UI server-side HTML rendering, Tabulator 6.x progressive enhancement, xUnit + FluentAssertions, README docs

---

### Task 1: Add shared breadcrumbs and remove the top query form

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing renderer and endpoint tests**

Add tests that assert:
- index pages render a breadcrumb shell linking back to `/`
- list pages render breadcrumbs like mount → entity
- edit/create pages render breadcrumbs like mount → entity → page mode
- list pages no longer render `efui-query-builder`, `efui-query-builder-form`, or `data-role="efui-query-form"`
- list pages render a compact table status area for active filters/sorts/errors instead of the old top form
- related-row query URLs such as `MediaTypeId eq 1` remain visibly represented in the list page output via the new status area

**Step 2: Run the focused tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_index|Get_entity_page|Get_chinook|Get_tracks"
```

Expected: FAIL because the renderer still outputs the top query form and has no breadcrumb markup.

**Step 3: Implement the minimal breadcrumb and status-strip renderer changes**

Update `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` to:
- introduce shared breadcrumb helpers for index, list, and form pages
- derive a readable mount label from `routePrefix`
- render a compact breadcrumb bar near the top of the page shell
- remove `RenderQueryBuilder` and all top-form markup
- replace it with a small non-form table status section that renders:
  - active filter chips/text
  - active sort chips/text
  - validation errors when present
- keep the create action, enhancement host, fallback table, FK links, and row actions intact

Keep the first pass intentionally small: no new renderer interface overloads unless needed.

**Step 4: Run the focused tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_index|Get_entity_page|Get_chinook|Get_tracks"
```

Expected: PASS with breadcrumbs present, top form gone, and active query state visible in the compact table status area.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: add breadcrumbs and simplify list chrome"
```

### Task 2: Enrich the server payload for column-owned Tabulator interactions

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing tests for richer table config**

Extend renderer/endpoint tests to assert that the emitted `data-role="efui-table-config"` JSON now contains enough metadata for Tabulator to own in-column interactions:
- `field`
- `title`
- `headerSort` / sortable capability
- `headerFilter` / filterable capability for data columns
- filter operator metadata (`contains` or `eq` where needed)
- active sort state
- active header filter values
- synthetic actions column metadata with filtering/sorting disabled
- a stable list URL or route context if needed by the client layer

Also assert that the status-strip representation and JSON payload stay aligned for URLs like:
- `/simple/users?filter.0.field=Name&filter.0.op=contains&filter.0.value=Ada`
- `/chinook/tracks?filter.0.field=MediaTypeId&filter.0.op=eq&filter.0.value=1`

**Step 2: Run the focused tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "query|tracks|table"
```

Expected: FAIL because the current config only exposes basic columns plus actions and does not encode header-filter-specific metadata.

**Step 3: Implement the minimal payload changes**

Update `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` so `BuildTableEnhancementConfig(...)` emits richer column/query metadata.

For each data column, include:
- sortable capability
- filterable capability
- header filter type for the first pass (text input is enough)
- the operator the client should encode back into the URL contract
- initial filter value when active

For the actions column, include explicit no-sort/no-filter metadata.

Keep the canonical query model unchanged. The payload should adapt the current server-owned `RenderedListView` and query state without inventing a new API contract.

**Step 4: Run the focused tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "query|tracks|table"
```

Expected: PASS with richer column metadata and active query state present in the enhancement config.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: enrich tabulator column metadata"
```

### Task 3: Move sorting/filtering and loading UX into Tabulator columns

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiTableAssets.cs`
- Modify: `src/EfUi.AspNetCore/EfUiFormCss.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing enhancement-asset tests**

Add/adjust tests to assert the table asset now wires real in-column interactions using Tabulator-friendly concepts:
- built-in header sorting instead of custom label hacks
- built-in header filters (`headerFilter`, `initialHeaderFilter`, or equivalent)
- a debounced URL-navigation path for header filter changes
- a Tabulator-backed loading indicator or loader configuration for navigation feedback
- no query-form submission handling anymore
- actions column explicitly excluded from sorting/filtering

Keep the tests string-based if needed, but make them specific enough to catch regressions.

**Step 2: Run the focused tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_table_enhancement_assets|Get_entity_page_renders_active|Get_tracks_list_filters_fk"
```

Expected: FAIL because the current asset still depends on the removed top form and does not yet use built-in Tabulator header filters.

**Step 3: Implement the minimal client enhancement changes**

Update `src/EfUi.AspNetCore/EfUiTableAssets.cs` to:
- build Tabulator columns from the richer server payload
- enable built-in header sorting for sortable columns
- enable built-in header filters for filterable columns
- hydrate initial sort and initial header filter state from the payload
- translate header sort and header filter changes back into the canonical URL contract
- debounce filter-driven navigation enough to avoid a request per keystroke
- reset `offset` to `0` on filter/sort changes and preserve `limit`
- use Tabulator’s loading/data-loader presentation where practical before navigating
- keep fallback hiding only after successful enhancement bootstrap

Update `src/EfUi.AspNetCore/EfUiFormCss.cs` to style:
- breadcrumbs
- compact table status strip
- refined table header/filter chrome
- loading state

Do not add client-owned row filtering/sorting semantics beyond producing the next server URL.

**Step 4: Run the focused tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_table_enhancement_assets|Get_entity_page_renders_active|Get_tracks_list_filters_fk"
```

Expected: PASS with the enhancement script now owning column interactions and loading feedback.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiTableAssets.cs src/EfUi.AspNetCore/EfUiFormCss.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: move list interactions into tabulator columns"
```

### Task 4: Update docs and verify related-row/query behavior end to end

**Files:**
- Modify: `README.md`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Review: `docs/plans/2026-05-19-tabulator-column-filters-and-breadcrumbs-design.md`

**Step 1: Write or tighten the failing documentation/end-to-end assertions**

Add or adjust tests so they prove the new intended behavior end to end:
- breadcrumbs render on `/simple`, `/simple/users`, `/simple/users/{id}/edit`, `/chinook`, and `/chinook/tracks`
- the old query form markup is absent from list pages
- related-row links still land on correctly filtered child pages
- FK `eq` filtering still returns rows for direct URLs like `MediaTypeId=1`
- active related-row filter state is exposed via the compact status strip and enhancement config

Update `README.md` expectations to document:
- breadcrumb navigation
- column-owned Tabulator filtering/sorting
- server-owned query semantics and URL contract
- compact status strip / fallback behavior
- loading feedback in the grid area

**Step 2: Run the focused docs/end-to-end tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "simple|chinook|tracks|invoice_items"
```

Expected: FAIL until docs/tests reflect the new breadcrumb and in-column interaction model.

**Step 3: Implement the minimal documentation and expectation updates**

Update `README.md` to remove references to the old top query-builder form and replace them with the new list UX description.

Keep the README focused on externally observable behavior, not internal renderer details.

**Step 4: Run the focused tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "simple|chinook|tracks|invoice_items"
```

Expected: PASS with docs/tests aligned to the new UX.

**Step 5: Commit**

```bash
git add README.md tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "docs: describe breadcrumb and column filter list ux"
```

### Task 5: Final verification and manual smoke pass

**Files:**
- Review: `docs/plans/2026-05-19-tabulator-column-filters-and-breadcrumbs-design.md`
- Review: `docs/plans/2026-05-19-tabulator-column-filters-and-breadcrumbs-implementation.md`

**Step 1: Run the repo verification commands**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
dotnet test EfUi.sln
```

Expected: PASS for all commands.

**Step 2: Run a manual sample-host smoke check**

Run:
```bash
dotnet run --project src/EfUi.SampleHost/EfUi.SampleHost.csproj
```

Verify manually in a browser:
- `/simple/users` shows breadcrumbs and no top form
- Tabulator column headers sort in place and navigate to the next server URL
- header filters visibly live in the columns
- a loading indicator appears in the grid area during navigation
- `/chinook/tracks?filter.0.field=MediaTypeId&filter.0.op=eq&filter.0.value=1` shows rows
- edit/delete actions still appear in the enhanced table
- `/simple`, `/chinook`, and edit pages expose breadcrumb navigation back upward

**Step 3: Record verification outcome**

If any verification step fails, stop and fix the issue before presenting completion.

**Step 4: Commit any remaining polish if needed**

```bash
git add -A
git commit -m "chore: finish breadcrumb and tabulator list polish"
```

Only create this commit if a real code or doc change was necessary after verification.
