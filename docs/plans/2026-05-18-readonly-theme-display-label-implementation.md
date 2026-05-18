# Readonly Theme and Shared Display Label Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Extend EF UI styling to index and readonly list pages while routing all human-facing related-entity labels through one shared `Name -> Title -> Email -> primary key` convention.

**Architecture:** Keep EF-aware lookup building in `EfUi.AspNetCore`, move the shared object-label rule into a reusable core helper, and make `HtmlPageRenderer` render themed index/list markup from prepared display values instead of doing FK-specific logic itself. Implement in TDD order: shared label resolution, prepared list rendering, ASP.NET list preparation, then endpoint regressions.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, Entity Framework Core metadata, xUnit, FluentAssertions.

---

## Preconditions

- Worktree: `.worktrees/readonly-theme-display-label`
- Branch: `feature/readonly-theme-display-label`
- Baseline verification already run in the worktree:
  - `dotnet restore EfUi.sln`
  - `dotnet test EfUi.sln --no-restore`
  - Result: 78 passing, 0 failing

## Implementation Notes

- Follow TDD strictly: write the failing test, run it and watch it fail for the expected reason, then write minimal code.
- Keep the shared display-label rule simple for now. Do not add per-entity configuration.
- Do not make `HtmlPageRenderer` depend on EF metadata resolution.
- Prefer small commits after each green step.
- All commands below should be run from `.worktrees/readonly-theme-display-label`.

### Task 1: Add failing tests for the shared display-label resolver

**Files:**
- Create: `src/EfUi.Core/Rendering/EntityDisplayLabelResolver.cs`
- Create: `tests/EfUi.Core.Tests/Rendering/EntityDisplayLabelResolverTests.cs`

**Step 1: Write the failing test**

Create `tests/EfUi.Core.Tests/Rendering/EntityDisplayLabelResolverTests.cs` with focused tests for:
- choosing `Name` before other candidates
- falling back to `Title`
- falling back to `Email`
- falling back to the primary key string when no preferred property is usable
- ignoring null / empty / whitespace values

Use small private row types inside the test file, for example:

```csharp
[Fact]
public void Resolve_prefers_name_then_title_then_email_then_primary_key()
{
    var row = new NamedRow { Id = 7, Name = "Ada" };

    var label = EntityDisplayLabelResolver.Resolve(row, primaryKeyPropertyName: "Id");

    label.Should().Be("Ada");
}
```

**Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~EntityDisplayLabelResolverTests
```

Expected: FAIL because `EntityDisplayLabelResolver` does not exist yet.

**Step 3: Write minimal implementation**

Create `src/EfUi.Core/Rendering/EntityDisplayLabelResolver.cs` with one public static method:

```csharp
public static string Resolve(object row, string primaryKeyPropertyName)
```

Implementation rules:
- probe `Name`, `Title`, `Email` via reflection in that order
- return the first non-blank string
- otherwise read the primary key property and format it with the same null-safe behavior used elsewhere

**Step 4: Run test to verify it passes**

Run the same test command again.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/EntityDisplayLabelResolver.cs tests/EfUi.Core.Tests/Rendering/EntityDisplayLabelResolverTests.cs
git commit -m "test: add shared entity display label resolver"
```

### Task 2: Add failing renderer tests for themed index and list pages

**Files:**
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Modify: `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Create: `src/EfUi.Core/Rendering/RenderedListRow.cs`

**Step 1: Write the failing tests**

Add focused tests to `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs` for:
- index pages linking `href="/efui/assets/efui.css"`
- index pages rendering semantic shell classes such as `efui-body`, `efui-page`, `efui-surface`, `efui-index-list`, `efui-link-grid`, or the final chosen class names
- list pages linking the same stylesheet and rendering semantic table classes
- list pages rendering prepared display values instead of raw object property access

Prefer introducing a dedicated list-row input model in the tests so the renderer no longer has to derive display values itself. Example test shape:

```csharp
var rows = new[]
{
    new RenderedListRow(
        Key: "1",
        Cells: new Dictionary<string, string>
        {
            ["ArtistId"] = "AC/DC",
            ["Title"] = "For Those About To Rock"
        })
};
```

**Step 2: Run targeted tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~HtmlPageRendererTests
```

Expected: FAIL because the renderer contract and markup do not yet support themed index/list pages or prepared list rows.

**Step 3: Write minimal implementation**

Implement the smallest change set that makes the tests pass:
- create `src/EfUi.Core/Rendering/RenderedListRow.cs`
- update `IHtmlPageRenderer` and `HtmlPageRenderer.RenderList(...)` to accept prepared list rows
- update `RenderIndex(...)` and `RenderList(...)` to emit the stylesheet link and semantic page/table classes
- keep existing action links and route escaping behavior intact

**Step 4: Run targeted tests to verify they pass**

Run the same renderer test command again.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/IHtmlPageRenderer.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Rendering/RenderedListRow.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: theme readonly index and list rendering"
```

### Task 3: Add failing ASP.NET tests for shared label usage on list pages

**Files:**
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`

**Step 1: Write the failing tests**

Add endpoint tests covering:
- `/simple` index page links the stylesheet and themed shell classes
- `/simple/users` list page links the stylesheet and themed table classes
- `/simple/users` displays `Admins` / `Guests` for the related group column instead of raw `1` / `2`
- `/chinook/albums` (or another stable FK-backed list) shows related labels such as artist names instead of raw IDs

Keep the assertions specific and avoid asserting the whole HTML document.

**Step 2: Run targeted endpoint tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests|FullyQualifiedName~ChinookEndpointsTests"
```

Expected: FAIL because list endpoints still use plain markup and raw row values.

**Step 3: Write minimal implementation**

In `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`:
- extract the existing `GetRelatedEntityLabel(...)` logic to use `EntityDisplayLabelResolver.Resolve(...)`
- add a helper that prepares `RenderedListRow` values for an entity list
- for each single-column FK property shown in the list, build one related-row lookup keyed by related PK string
- populate FK cells with the shared display label when lookup succeeds
- fall back to raw FK value when the related row is missing
- keep non-FK cells on the existing scalar formatting path
- update the list endpoint to pass prepared rows into `RenderList(...)`

**Step 4: Run targeted endpoint tests to verify they pass**

Run the same endpoint test command again.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: share display labels across list pages and forms"
```

### Task 4: Extend the stylesheet with readonly and index page theme classes

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiFormCss.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`

**Step 1: Write or tighten the failing assertions**

Add or refine tests so they assert for the final semantic classes chosen for:
- page shell
- surface/card wrapper
- action bar / primary action link
- table wrapper
- table element
- row action buttons / links

If Task 2 only asserted a subset of classes, add the missing assertions now before styling.

**Step 2: Run targeted tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~HtmlPageRendererTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests|FullyQualifiedName~ChinookEndpointsTests"
```

Expected: FAIL if the final class names are not all present yet.

**Step 3: Write minimal implementation**

Update `src/EfUi.AspNetCore/EfUiFormCss.cs` to add only the CSS needed for index/list pages, reusing the existing visual language:
- page shell containers
- navigation link grid
- list toolbar / create action
- table wrapper, table, header, cell, row states
- inline action area for Edit/Delete

Do not redesign forms while doing this step.

**Step 4: Run targeted tests to verify they pass**

Run the two targeted test commands again.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiFormCss.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "style: extend efui theme to readonly pages"
```

### Task 5: Add fallback coverage for missing labels and missing related rows

**Files:**
- Modify: `tests/EfUi.Core.Tests/Rendering/EntityDisplayLabelResolverTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`

**Step 1: Write the failing tests**

Add tests for:
- a resolver case where `Name` is whitespace and `Title` is used
- a resolver case where only `Email` exists
- a list-page case where an FK points to a missing related row and the raw FK value is shown instead of throwing
- a null FK case rendering as empty

**Step 2: Run targeted tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~EntityDisplayLabelResolverTests
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~EfUiEndpointsTests
```

Expected: FAIL until the fallback cases are implemented correctly.

**Step 3: Write minimal implementation**

Adjust resolver / lookup preparation code only as needed to satisfy the fallback tests. Avoid adding new configurability.

**Step 4: Run targeted tests to verify they pass**

Run the same two commands again.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.Core.Tests/Rendering/EntityDisplayLabelResolverTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "test: cover display label fallbacks"
```

### Task 6: Final verification before any completion claim

**Files:**
- Verify only

**Step 1: Run the full test suite**

Run:

```bash
dotnet test EfUi.sln --no-restore
```

Expected: PASS with 0 failures.

**Step 2: Inspect the diff**

Run:

```bash
git status --short
git diff -- src/EfUi.Core src/EfUi.AspNetCore tests/EfUi.Core.Tests tests/EfUi.AspNetCore.Tests
```

Expected: only the intended files for shared display labels and readonly theming are changed.

**Step 3: Optional manual verification**

Run the sample host and manually inspect:
- `/simple`
- `/simple/users`
- `/simple/groups`
- `/chinook/albums`
- `/chinook/playlists/1/edit`

Confirm:
- index/list pages share the theme
- list FK values show labels
- forms still show the same related labels as before

**Step 4: Commit remaining changes**

```bash
git add src/EfUi.Core src/EfUi.AspNetCore tests/EfUi.Core.Tests tests/EfUi.AspNetCore.Tests docs/plans/2026-05-18-readonly-theme-display-label-implementation.md
git commit -m "feat: theme readonly pages and unify display labels"
```

**Step 5: Only then report status**

Do not claim success until the full `dotnet test EfUi.sln --no-restore` command has been run fresh and the output confirms 0 failures.
