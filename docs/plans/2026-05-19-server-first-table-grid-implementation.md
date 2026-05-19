# Server-First Table Grid Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add a server-first list/query system with visible editable filters and sorts, richer FK-aware table cells, related-row prefilters, and a Tabulator enhancement path without giving the client library ownership of EF UI’s table semantics.

**Architecture:** Keep the ASP.NET list endpoints authoritative. Introduce a small canonical `TableQuery` contract, enrich list rendering beyond string-only cells, and render a query-builder bar above the current table markup. Execute filtering/sorting on the server first, then layer Tabulator on top as a replaceable enhancement that consumes server-prepared data and query state.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core metadata/querying, server-rendered HTML in `EfUi.Core`, xUnit + FluentAssertions test projects, Tabulator (client enhancement only)

---

### Task 1: Introduce a richer list rendering model

**Files:**
- Create: `src/EfUi.Core/Rendering/RenderedListCell.cs`
- Create: `src/EfUi.Core/Rendering/RenderedListFilter.cs`
- Create: `src/EfUi.Core/Rendering/RenderedListSort.cs`
- Create: `src/EfUi.Core/Rendering/RenderedListView.cs`
- Modify: `src/EfUi.Core/Rendering/RenderedListRow.cs`
- Modify: `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`

**Step 1: Write the failing renderer tests**

Add renderer tests in `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs` that assert list rendering can accept a richer view model containing:
- visible filter state above the table
- visible sort state above the table
- cells rendered from `text` plus optional `href`
- FK label cells rendered as links when `href` is present
- existing row action markup still present

Also add a compatibility assertion proving there is still only one public list-rendering overload and that it now consumes the richer list-view object rather than raw `RenderedListRow` collections.

**Step 2: Run the focused renderer tests to verify they fail**

Run: `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests`

Expected: FAIL because list rendering still takes only string-only rows and has no query-builder shell.

**Step 3: Write the minimal rendering model and renderer implementation**

Create small rendering records under `src/EfUi.Core/Rendering/` for:
- list cell text + optional href
- rendered filter state
- rendered sort state
- list-level view model holding filters, sorts, rows, and any table metadata needed now

Update `src/EfUi.Core/Rendering/RenderedListRow.cs` so each cell can carry the richer rendered cell object instead of plain strings.

Update `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs` and `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` so list pages render:
- a query-builder container above the table
- the current filters and sorts as visible UI state
- linked FK cells when a cell `href` exists
- the existing action column markup unchanged

Keep the first pass HTML-first: the query-builder can initially render existing state plus add/remove controls without needing live behavior yet.

**Step 4: Run the focused renderer tests to verify they pass**

Run: `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests`

Expected: PASS with the richer list view model and query-builder shell rendered.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/RenderedListCell.cs src/EfUi.Core/Rendering/RenderedListFilter.cs src/EfUi.Core/Rendering/RenderedListSort.cs src/EfUi.Core/Rendering/RenderedListView.cs src/EfUi.Core/Rendering/RenderedListRow.cs src/EfUi.Core/Rendering/IHtmlPageRenderer.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: add richer list rendering model"
```

### Task 2: Add the canonical table query contract and URL binder

**Files:**
- Create: `src/EfUi.Core/Rendering/TableQuery.cs`
- Create: `src/EfUi.Core/Rendering/TableQueryField.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`

**Step 1: Write the failing endpoint/unit-style tests for query binding**

Add tests in `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` that exercise list URLs such as:
- `/simple/users?filter.0.field=Name&filter.0.op=contains&filter.0.value=Ada`
- `/simple/users?sort.0.field=Email&sort.0.dir=desc`
- `/simple/users?offset=0&limit=25`

Assert that the rendered page shows the active filters/sorts in the query-builder bar and that invalid field/operator combinations are rejected with a visible error state rather than crashing.

**Step 2: Run the focused ASP.NET tests to verify they fail**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "EfUiEndpointsTests|ChinookEndpointsTests"`

Expected: FAIL because the list endpoint ignores query-string filter/sort state and renders no visible query-builder values.

**Step 3: Write the minimal query contract and binding implementation**

Create `src/EfUi.Core/Rendering/TableQuery.cs` with EF UI’s canonical list-query records:
- `TableQuery`
- `TableFilterClause`
- `TableSortClause`

Create `src/EfUi.Core/Rendering/TableQueryField.cs` (or equivalent) to describe which fields are exposed by a given table and which operators/directions they allow.

Update `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs` so the list endpoint:
- reads query-string values from `HttpRequest`
- binds them into `TableQuery`
- validates field/operator/direction names against the exposed table definition
- surfaces validation errors into the rendered list view model
- preserves `offset` and `limit` in the rendered state

Do not add OData parsing. Keep the binder strictly aligned to the custom `filter.N.*` and `sort.N.*` URL contract from the design doc.

**Step 4: Run the focused ASP.NET tests to verify they pass**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "EfUiEndpointsTests|ChinookEndpointsTests"`

Expected: PASS with visible query-builder state and graceful handling of invalid query parts.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/TableQuery.cs src/EfUi.Core/Rendering/TableQueryField.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "feat: bind url query state for list tables"
```

### Task 3: Execute server-side filtering and sorting for exposed columns

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.Core/Metadata/EntityMetadata.cs`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing end-to-end list behavior tests**

Extend the ASP.NET tests to prove:
- scalar `contains` filtering narrows the `/simple/users` list on the server
- scalar sorting changes row order on the server
- the filtered/sorted state remains visible in the query-builder bar
- FK display columns already shown as labels (for example Chinook albums → artist label) can be filtered and sorted by the displayed label semantics rather than only raw FK ids

Use HTML row-order assertions rather than implementation details.

**Step 2: Run the focused ASP.NET tests to verify they fail**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_page|Get_albums_list|query"`

Expected: FAIL because the current list endpoint always loads the full set and does not apply any `TableQuery` operations.

**Step 3: Write the minimal server-side query execution implementation**

Update `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs` so list endpoints translate the validated `TableQuery` into EF-backed filtering and sorting before row rendering.

Update `src/EfUi.Core/Metadata/EntityMetadata.cs` and `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs` as needed so the list pipeline knows:
- which columns are filterable/sortable
- which displayed FK label columns map back to related entities for label-based query execution

Implement only the first required operators/directions from the design:
- `contains`
- equality where needed for pre-applied related-row filters
- ascending / descending sorts

Do not add arbitrary relationship traversal or free-form expressions.

**Step 4: Run the focused ASP.NET tests to verify they pass**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "EfUiEndpointsTests|ChinookEndpointsTests"`

Expected: PASS with server-side filter/sort behavior reflected in rendered rows.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Metadata/EntityMetadata.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: execute server-side list filters and sorts"
```

### Task 4: Add FK edit links and related-row prefilter navigation

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.Core/Metadata/RelatedEntityManagementLink.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing navigation/link tests**

Add endpoint tests that assert:
- FK label cells on list pages render links to the related row edit page
- the existing “Manage related rows” links now target the child list with a pre-applied query-string filter
- the destination child list renders that pre-applied filter visibly in the query-builder bar
- removing/changing the filter in the URL returns the normal unfiltered list behavior

Use concrete routes from `/simple` and `/chinook` where possible.

**Step 2: Run the focused endpoint tests to verify they fail**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "related|albums|users"`

Expected: FAIL because FK cells are still plain text and related-row links still point only to base child routes.

**Step 3: Write the minimal navigation implementation**

Update list-row building in `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs` so FK display cells carry:
- the rendered label text
- an edit-page href using the related row key

Update `src/EfUi.Core/Metadata/RelatedEntityManagementLink.cs` if needed so related-row links can carry enough information to build a child-table prefilter URL.

Update `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` so:
- FK cells render as anchors when an href exists
- related-row links include the child-list query-string filter rather than a bare route

Keep the pre-applied filter aligned with the same `TableQuery` URL contract from Task 2.

**Step 4: Run the focused endpoint tests to verify they pass**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "EfUiEndpointsTests|ChinookEndpointsTests"`

Expected: PASS with clickable FK labels and visible related-row prefilters.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Metadata/RelatedEntityManagementLink.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: link fk cells and related row filters"
```

### Task 5: Add the Tabulator enhancement layer without changing server ownership

**Files:**
- Create: `src/EfUi.AspNetCore/EfUiTableAssets.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.AspNetCore/EfUiFormCss.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `README.md`

**Step 1: Write the failing enhancement tests**

Add endpoint tests that assert list pages now include:
- the JS/CSS assets required for the table enhancement
- data attributes or JSON payload needed to bootstrap Tabulator from server-owned data
- a non-enhanced HTML table fallback still present in the page output

Also add a documentation assertion/update target in `README.md` for the new list capabilities and URL contract.

**Step 2: Run the focused endpoint tests to verify they fail**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "table|assets|query"`

Expected: FAIL because there is no dedicated table enhancement asset path or bootstrap markup yet.

**Step 3: Write the minimal enhancement integration**

Create `src/EfUi.AspNetCore/EfUiTableAssets.cs` to hold the local asset strings or bootstrap payload for Tabulator-related enhancement.

Update `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs` to serve the table enhancement asset route(s) alongside `efui.css`.

Update `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` so list pages emit:
- a stable container for Tabulator enhancement
- server-prepared column/row/query-state data for bootstrapping
- the existing HTML table fallback

Update `src/EfUi.AspNetCore/EfUiFormCss.cs` for the query-builder and enhancement shell styles.

Document in `README.md`:
- the URL query contract
- server-owned filter/sort behavior
- FK link behavior
- related-row prefilter behavior
- progressive enhancement expectations

**Step 4: Run the focused tests and broader verification**

Run:
- `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
- `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`
- `dotnet test EfUi.sln`

Expected: PASS for all commands.

**Step 5: Manual verification**

Run the sample host and verify in a browser:
- `/simple/users` shows the query-builder bar above the list
- filters and sorts update results through URL-driven server behavior
- FK label cells are clickable and open edit pages
- related-row links open child lists with visible editable filters
- the page still renders useful fallback HTML if JS enhancement is disabled
- the table enhancement does not break current row actions

Suggested run command:

```bash
dotnet run --project src/EfUi.SampleHost/EfUi.SampleHost.csproj
```

**Step 6: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiTableAssets.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.AspNetCore/EfUiFormCss.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs README.md
git commit -m "feat: enhance server-first list pages with tabulator"
```

### Task 6: Final verification and quality pass

**Files:**
- Review: `docs/plans/2026-05-19-server-first-table-grid-design.md`
- Review: `docs/plans/2026-05-19-server-first-table-grid-implementation.md`
- Review: `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.md`
- Review: `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.json`

**Step 1: Run the repo verification commands**

Run:
- `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
- `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`
- `dotnet test EfUi.sln`

Expected: PASS for all commands.

**Step 2: Run the repo Sonar task**

Run: `mise run sonar`

Expected: Sonar scan completes and refreshes the local summary artifacts.

**Step 3: Inspect the local Sonar summaries**

Review:
- `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.md`
- `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.json`

Expected: no new security findings and no new unresolved high-impact findings caused by the table-grid work.

**Step 4: Record the final verification outcome**

If the implementation commits are already in place, keep the branch ready for the finishing-a-development-branch workflow. If any verification step fails, stop and fix the failures before presenting completion.
