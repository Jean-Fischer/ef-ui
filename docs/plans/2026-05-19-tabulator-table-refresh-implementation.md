# Tabulator Table Refresh Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Remove the page-refresh feel from list filtering by making Tabulator the only visible table surface, eliminating the standalone loading banner, and refreshing rows in place only when the user commits a filter or sort.

**Architecture:** Keep the server-rendered page shell and the existing `/data` JSON endpoint authoritative, but make the client lean on Tabulator for the visible grid, filter controls, and loading affordance. The browser should stay on the same document, the URL should still reflect the query state, and the table should update in place without a page-blink effect. The fallback HTML table remains available when enhancement is unavailable.

**Tech Stack:** ASP.NET Core minimal APIs, server-rendered HTML, Tabulator 6.x, xUnit, FluentAssertions.

---

### Task 1: Remove the standalone loading banner from list pages

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `src/EfUi.AspNetCore/EfUiFormCss.cs`
- Modify: `src/EfUi.AspNetCore/EfUiTableAssets.cs`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing tests**
- Update the list renderer tests so they assert the enhancement shell no longer renders `data-role="efui-table-loading"`.
- Update the asset tests so they assert the CSS and JS no longer depend on the separate banner via `.efui-table-loading`, `setLoading(`, or `efui-table-host-loading`.
- Keep assertions that Tabulator still advertises its own in-grid loader text via `dataLoaderLoading` / `dataLoaderError`.

**Step 2: Run test(s) to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_table_enhancement_assets_expose_tabulator_bootstrap_shell|Get_entity_page_renders_active_query_state_from_url_in_compact_status_area|Get_tracks_list_filters_fk_rows_by_raw_key_when_eq_query_comes_from_url_contract"
```

Expected: FAIL because the current renderer still emits the extra loading banner and the asset still manages a separate loading element.

**Step 3: Write minimal implementation**
- Remove the extra banner element from `RenderTableEnhancementShell` in `HtmlPageRenderer`.
- Delete the `.efui-table-loading` CSS block from `EfUiFormCss`.
- Remove the banner-manipulation branch from `EfUiTableAssets` so the client only relies on Tabulator’s own loader/error presentation.

**Step 4: Run test(s) to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_table_enhancement_assets_expose_tabulator_bootstrap_shell|Get_entity_page_renders_active_query_state_from_url_in_compact_status_area|Get_tracks_list_filters_fk_rows_by_raw_key_when_eq_query_comes_from_url_contract"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.AspNetCore/EfUiFormCss.cs src/EfUi.AspNetCore/EfUiTableAssets.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "fix: remove standalone table loading banner"
```

---

### Task 2: Make Tabulator filter changes commit explicitly and refresh in place

**Files:**
- Modify: `src/EfUi.Core/Rendering/RenderedListPayloadFactory.cs`
- Modify: `src/EfUi.AspNetCore/EfUiTableAssets.cs`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing tests**
- Add assertions that list payload columns now include `headerFilterLiveFilter: false` for filterable columns.
- Add assertions that the Tabulator asset no longer contains the live-filter debounce path (`headerFilterLiveFilterDelay: 400`, `clearTimeout(filterNavigationHandle)`, or the `setTimeout(function () { ... }, 400)` refresh wrapper).
- Add assertions that the asset still uses the existing JSON endpoint and URL sync flow (`fetch(`, `history.replaceState`, `popstate`) and does not fall back to a full page navigation path.

**Step 2: Run test(s) to verify they fail**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Get_table_enhancement_assets_expose_tabulator_bootstrap_shell
```

Expected: FAIL because the current payload and client still assume the live/debounced filter path.

**Step 3: Write minimal implementation**
- Add `headerFilterLiveFilter: false` to the emitted column metadata in `RenderedListPayloadFactory` for filterable columns.
- Update `EfUiTableAssets` so committed filter changes trigger a single in-place refresh instead of a debounced reload loop.
- Keep the existing URL contract, `fetch(...)` JSON refresh, and `history.replaceState(...)` synchronization intact.
- Leave sort refresh behavior on the same in-place path so sorting and filtering use one client mechanism.

**Step 4: Run test(s) to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter Get_table_enhancement_assets_expose_tabulator_bootstrap_shell
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/RenderedListPayloadFactory.cs src/EfUi.AspNetCore/EfUiTableAssets.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: make tabulator filter commits explicit"
```

---

### Task 3: Update docs and run full verification

**Files:**
- Modify: `README.md`
- Verify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the doc/update expectations**
- Update the README list-behavior bullets so they describe the list page as a single table surface with Tabulator-owned header filters and an in-grid loading state.
- Remove any wording that implies a separate loading banner or query-builder-style refresh UX.
- If any test expectations drift while implementing the client changes, add the missing regression assertions before finishing.

**Step 2: Run focused verification**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: PASS.

**Step 3: Run the full solution verification**

Run:
```bash
dotnet test EfUi.sln
```

Expected: PASS.

**Step 4: Manual smoke test**
- Run the sample host.
- Verify that applying a header filter updates rows without a page blink.
- Verify that the URL updates, back/forward still works, and the fallback table remains in the HTML source.
- Verify that the table shows loading inside the grid rather than as a page-level banner.

**Step 5: Commit**

```bash
git add README.md
git commit -m "docs: describe tabulator in-place table refresh"
```
