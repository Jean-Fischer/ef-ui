# FK Display Column Customization Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Let users choose which property is shown for foreign-key related values, with per-navigation overrides and entity-level defaults, while keeping the current heuristic fallback.

**Architecture:** Extend EF UI metadata discovery to resolve a display-property name for each relationship from a lightweight attribute model, then reuse that resolved metadata everywhere a related value is rendered. The feature stays attribute-based, fails open to the existing heuristic, and does not require fluent API or partial configuration classes.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core metadata, server-rendered HTML, xUnit, FluentAssertions.

---

### Task 1: Add the attribute and resolved metadata fields

**Files:**
- Create: `src/EfUi.Core/Metadata/EfUiDisplayColumnAttribute.cs`
- Modify: `src/EfUi.Core/Metadata/EntityPropertyMetadata.cs`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Test: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`

**Step 1: Write the failing tests**
- Add tests showing a navigation-property attribute overrides the default display column.
- Add tests showing a class-level attribute applies when no navigation override exists.
- Add tests showing missing/invalid display columns fall back safely.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter EntityMetadataProviderTests
```

Expected: FAIL because the attribute and resolved metadata do not exist yet.

**Step 3: Write minimal implementation**
- Add `EfUiDisplayColumnAttribute` with a single `PropertyName` constructor parameter.
- Extend `EntityPropertyMetadata` with a nullable `RelatedDisplayPropertyName` field.
- Update `EfEntityMetadataProvider` to resolve:
  - navigation-property attribute
  - entity-level attribute
  - current heuristic fallback
  - primary key fallback
- Store the resolved display-property name in metadata for related properties.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter EntityMetadataProviderTests
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Metadata/EfUiDisplayColumnAttribute.cs src/EfUi.Core/Metadata/EntityPropertyMetadata.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs
git commit -m "feat: add fk display column metadata"
```

---

### Task 2: Use the resolved display column everywhere related values are shown

**Files:**
- Modify: `src/EfUi.Core/Rendering/EntityDisplayLabelResolver.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing tests**
- Add a renderer test proving list cells use the configured related display property.
- Add endpoint tests proving FK labels in list pages and related-row pickers use the overridden display column.
- Keep a regression test for heuristic fallback when no attribute is present.

**Step 2: Run tests to verify they fail**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_page_renders_active_query_state_from_url_in_compact_status_area|Get_tracks_list_filters_fk_rows_by_raw_key_when_eq_query_comes_from_url_contract|Get_invoice_items_list_shows_prefilter_from_related_rows_link_as_visible_query_state"
```

Expected: FAIL because rendering still uses the existing heuristic only.

**Step 3: Write minimal implementation**
- Change the resolver to accept an optional display-property name.
- Use the resolved display-property name for list cell text, links, related-row labels, and picker labels.
- Keep the old heuristic as the fallback chain.

**Step 4: Run tests to verify they pass**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "Get_entity_page_renders_active_query_state_from_url_in_compact_status_area|Get_tracks_list_filters_fk_rows_by_raw_key_when_eq_query_comes_from_url_contract|Get_invoice_items_list_shows_prefilter_from_related_rows_link_as_visible_query_state"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/EntityDisplayLabelResolver.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: use fk display column overrides"
```

---

### Task 3: Update docs and run full verification

**Files:**
- Modify: `README.md`
- Modify: `docs/plans/2026-05-19-fk-display-column-customization-design.md` if needed for wording alignment
- Verify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Verify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Update docs if needed**
- Mention the new attribute in the README or relevant design notes.
- Keep the docs concise and focused on the new attribute-based override model.

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
git add README.md docs/plans/2026-05-19-fk-display-column-customization-design.md
git commit -m "docs: describe fk display column customization"
```
