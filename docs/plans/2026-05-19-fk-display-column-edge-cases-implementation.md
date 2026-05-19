# FK Display Column Edge Cases Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add graceful, user-visible reporting for FK display-column edge cases, while keeping the feature simple and supporting only the low-complexity cases we explicitly chose.

**Architecture:** Keep the current attribute-based FK display model, but extend metadata discovery to collect non-fatal issues and expose them through a compact error summary when a page can still render. Support the simple scalar FK fallback path when a relationship has no navigation property but does have a CLR FK property. Treat keyless entities as read-only candidates only if we can derive a stable synthetic identity; otherwise report them clearly and skip them. Avoid large redesigns, fluent API, or new configuration subsystems.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core metadata, server-rendered HTML, xUnit, FluentAssertions.

---

### Task 1: Add edge-case diagnostics to metadata discovery

**Files:**
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Modify: `src/EfUi.Core/Metadata/EntityMetadata.cs`
- Modify: `src/EfUi.Core/Metadata/EditableFieldMetadata.cs`
- Test: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`

**Step 1: Write the failing tests**
- Add tests that assert metadata discovery collects warnings instead of throwing for:
  - unsupported composite primary keys
  - composite foreign keys
  - shadow FK-only relationships with no CLR FK property
- Add tests that assert supported cases still succeed:
  - per-navigation override
  - class-level default
  - scalar FK fallback when there is no navigation but there is a CLR FK property
- Add tests for a keyless entity scenario, expecting either:
  - a read-only friendly synthetic identity path, if implemented, or
  - a clear omission/warning record if not supported.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter EntityMetadataProviderTests
```

Expected: FAIL because diagnostics and the fallback rules are not fully implemented yet.

**Step 3: Write minimal implementation**
- Extend metadata models with a compact diagnostics list or warning collection.
- Make metadata discovery fail open where practical.
- Preserve the current hard reject for composite PKs if the UI cannot render them safely, but capture the reason.
- Support scalar FK fallback when a CLR FK property exists but no navigation is available.
- Record a keyless-entity diagnostic instead of silently skipping it.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter EntityMetadataProviderTests
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs src/EfUi.Core/Metadata/EntityMetadata.cs src/EfUi.Core/Metadata/EditableFieldMetadata.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs
git commit -m "feat: collect fk display edge case diagnostics"
```

---

### Task 2: Surface edge-case warnings in the rendered UI

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.Core/Rendering/RenderedListPayloadFactory.cs`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing tests**
- Add tests asserting a warning summary renders when edge-case diagnostics are present.
- Add tests asserting the warning summary lists entity/relationship names plus reasons.
- Add tests asserting a dedicated error page is shown only when nothing usable can render.
- Add tests asserting the scalar FK fallback still displays reasonable labels.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_page_renders_active_query_state_from_url_in_compact_status_area|Get_tracks_list_filters_fk_rows_by_raw_key_when_eq_query_comes_from_url_contract"
```

Expected: FAIL because the UI does not yet surface the diagnostics.

**Step 3: Write minimal implementation**
- Add a compact warning summary block to the page shell.
- Render the warning summary only when there are diagnostics and some content can still render.
- Render a dedicated error page only when the page cannot render anything usable.
- Keep the wording brief and actionable.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_page_renders_active_query_state_from_url_in_compact_status_area|Get_tracks_list_filters_fk_rows_by_raw_key_when_eq_query_comes_from_url_contract"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Rendering/RenderedListPayloadFactory.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: surface fk edge case warnings"
```

---

### Task 3: Update docs and run full verification

**Files:**
- Modify: `README.md`
- Modify: `docs/plans/2026-05-19-fk-display-column-edge-cases.md`
- Verify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Verify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Update docs**
- Mention that unsupported edge cases can surface as warnings or a dedicated error page.
- Keep the README and edge-case doc aligned with the chosen shortlist.

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

**Step 4: Commit**

```bash
git add README.md docs/plans/2026-05-19-fk-display-column-edge-cases.md
git commit -m "docs: describe fk edge case reporting"
```
