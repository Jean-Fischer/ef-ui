# Forms Chip-Picker Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add a clean admin-style form theme and replace the current many-to-many checkbox wall with a JavaScript-enhanced chip picker while preserving the existing repeated-value POST contract.

**Architecture:** Keep EF UI server-rendered. Add a local stylesheet endpoint under each EF UI route prefix for semantic form classes, keep the no-build constraint, and implement the chip-picker as progressive enhancement over a simple checkbox fallback. JavaScript stays inline, minimal, and scoped to collection fields only.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, server-rendered HTML, xUnit, FluentAssertions, vanilla JavaScript, local CSS served by the EF UI middleware.

---

### Task 1: Add form theming scaffold and local stylesheet route

**Files:**
- Create: `src/EfUi.AspNetCore/EfUiFormCss.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs:64-163`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:31-43`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing tests**

Add a renderer test asserting that edit forms now include a stylesheet link and semantic form classes.

```csharp
[Fact]
public void RenderEditForm_includes_form_theme_stylesheet_and_semantic_classes()
{
    var sut = new HtmlPageRenderer();
    // arrange metadata similar to existing scalar-field tests

    var html = sut.RenderEditForm("/efui", metadata, model, isCreate: false, errors: new Dictionary<string, string[]>(), key: 7);

    html.Should().Contain("href=\"/efui/assets/efui.css\"");
    html.Should().Contain("class=\"efui-form\"");
    html.Should().Contain("class=\"efui-field\"");
    html.Should().Contain("class=\"efui-input\"");
}
```

Add an ASP.NET endpoint test asserting the form page links the local stylesheet.

```csharp
[Fact]
public async Task Get_playlist_edit_form_links_local_form_stylesheet()
{
    var html = await _client.GetStringAsync("/chinook/playlists/1/edit");
    html.Should().Contain("href=\"/chinook/assets/efui.css\"");
}
```

**Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests.RenderEditForm_includes_form_theme_stylesheet_and_semantic_classes"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests.Get_playlist_edit_form_links_local_form_stylesheet"
```

Expected: FAIL because there is no stylesheet link, no semantic form classes, and no stylesheet endpoint yet.

**Step 3: Write the minimal implementation**

Create `src/EfUi.AspNetCore/EfUiFormCss.cs` with one exported CSS string containing the first-pass clean-admin theme.

```csharp
namespace EfUi.AspNetCore;

internal static class EfUiFormCss
{
    internal const string Content = """
.efui-form { max-width: 48rem; margin: 2rem auto; padding: 1.5rem; }
.efui-field { display: grid; gap: 0.5rem; margin-bottom: 1rem; }
.efui-label { font-weight: 600; color: #111827; }
.efui-input, .efui-select, .efui-search-input { width: 100%; border: 1px solid #d1d5db; border-radius: 0.5rem; padding: 0.625rem 0.75rem; }
.efui-error { color: #b91c1c; background: #fef2f2; border: 1px solid #fecaca; padding: 0.75rem; border-radius: 0.5rem; }
.efui-button { display: inline-flex; align-items: center; justify-content: center; border-radius: 0.5rem; padding: 0.625rem 1rem; background: #111827; color: white; }
""";
}
```

Map a GET route in `EfUiApplicationBuilderExtensions`:

```csharp
app.MapGet($"{options.RoutePrefix}/assets/efui.css", () =>
    Results.Text(EfUiFormCss.Content, "text/css"));
```

Update `HtmlPageRenderer.RenderEditForm` to:
- emit a `<head>` with the stylesheet link
- wrap form content in semantic containers
- add classes to labels, inputs, selects, errors, and submit button
- leave index/list pages untouched in this phase

**Step 4: Run tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests.RenderEditForm_includes_form_theme_stylesheet_and_semantic_classes"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests.Get_playlist_edit_form_links_local_form_stylesheet"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiFormCss.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: add efui form theme scaffold"
```

### Task 2: Replace collection field markup with chip-picker shell plus fallback

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs:164-205`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs:167-217`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs:38-82`

**Step 1: Write the failing tests**

Update the renderer test so it expects a chip-picker shell instead of the old checkbox-only picker.

```csharp
html.Should().Contain("efui-chip-picker");
html.Should().Contain("efui-chip-picker-selected");
html.Should().Contain("efui-chip-picker-results");
html.Should().Contain("efui-chip-picker-fallback");
html.Should().Contain("data-role=\"chip-picker\"");
html.Should().Contain("name=\"Tracks\" type=\"checkbox\" value=\"1\" checked");
html.Should().NotContain("<select name=\"Tracks\" multiple>");
```

Update the endpoint test name and assertions accordingly.

```csharp
[Fact]
public async Task Get_playlist_edit_form_renders_tracks_chip_picker_shell()
{
    var html = await _client.GetStringAsync("/chinook/playlists/1/edit");
    html.Should().Contain("efui-chip-picker");
    html.Should().Contain("efui-chip-picker-fallback");
    html.Should().Contain("data-role=\"chip-picker-search\"");
}
```

**Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests.RenderEditForm_renders_collection_fields_as_chip_picker"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests.Get_playlist_edit_form_renders_tracks_chip_picker_shell"
```

Expected: FAIL because the current markup still uses `efui-collection-picker` and inline styles.

**Step 3: Write the minimal implementation**

Replace `RenderCollectionField` with semantic markup that includes:
- chip-picker outer shell
- selected-chip host
- search input
- results list host
- hidden-input host for JS mode
- checkbox fallback block for no-JS mode and initial state source

Use the existing `RelatedEntityOption` list as the source of truth.

```csharp
html.Append($"<div class=\"efui-field efui-chip-picker\" data-role=\"chip-picker\" data-field-name=\"{fieldName}\">");
html.Append("<div class=\"efui-chip-picker-selected\" data-role=\"chip-picker-selected\"></div>");
html.Append($"<input type=\"search\" class=\"efui-search-input\" data-role=\"chip-picker-search\" placeholder=\"Search {fieldName}...\" />");
html.Append("<div class=\"efui-chip-picker-results\" data-role=\"chip-picker-results\"></div>");
html.Append("<div class=\"efui-chip-picker-hidden-inputs\" data-role=\"chip-picker-hidden-inputs\"></div>");
html.Append("<div class=\"efui-chip-picker-fallback\">");
// existing checkbox rendering stays here as fallback / initialization source
```

**Step 4: Run tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests.RenderEditForm_renders_collection_fields_as_chip_picker"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests.Get_playlist_edit_form_renders_tracks_chip_picker_shell"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: add chip picker form markup"
```

### Task 3: Add minimal JavaScript enhancement for chip behavior

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs:188-205`
- Test: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs:167-257`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs:50-82`

**Step 1: Write the failing tests**

Add assertions that the rendered HTML now contains the expected script hooks for enhancement while preserving the POST contract.

```csharp
html.Should().Contain("data-role=\"chip-picker-hidden-inputs\"");
html.Should().Contain("data-role=\"chip-picker-results\"");
html.Should().Contain("document.addEventListener('DOMContentLoaded'");
```

Keep the existing POST integration tests for playlist updates. Those tests should stay unchanged and continue proving:
- repeated `Tracks` values update the relationship set
- omitting `Tracks` clears the set

**Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests.RenderEditForm_renders_collection_fields_as_chip_picker"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests.Post_update_playlist"
```

Expected: renderer test FAILS because the script and hidden-input synchronization hooks do not exist yet; POST tests should continue to PASS before the implementation.

**Step 3: Write the minimal implementation**

Replace the existing collection-filter script with a small initializer that:
- finds each chip picker on `DOMContentLoaded`
- reads fallback checkboxes as the initial option source
- builds a `selected` map from checked fallback inputs
- disables fallback checkbox inputs after initialization
- renders chips for selected values
- filters unselected options into the results list
- writes repeated hidden inputs for the selected set
- removes selections when the chip remove button is clicked

Skeleton:

```javascript
document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('[data-role="chip-picker"]').forEach(function (picker) {
    var fallbackInputs = Array.from(picker.querySelectorAll('.efui-chip-picker-fallback input[type="checkbox"]'));
    var state = fallbackInputs.map(function (input) {
      return { value: input.value, label: input.dataset.label || '', selected: input.checked };
    });

    function syncHiddenInputs() { /* render repeated hidden inputs named after data-field-name */ }
    function renderChips() { /* selected chips with remove buttons */ }
    function renderResults(query) { /* unselected options filtered by substring */ }

    fallbackInputs.forEach(function (input) { input.disabled = true; });
    syncHiddenInputs();
    renderChips();
    renderResults('');
  });
});
```

Do not add async search, keyboard combobox logic, or framework dependencies.

**Step 4: Run tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests.RenderEditForm_renders_collection_fields_as_chip_picker"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: enhance chip picker with minimal javascript"
```

### Task 4: Verify the full form-only refresh and do a manual smoke test

**Files:**
- Verify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Verify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Verify: `src/EfUi.AspNetCore/EfUiFormCss.cs`
- Verify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Verify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Run the focused automated verification**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~HtmlPageRendererTests"
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --no-restore --nologo -v minimal --filter "FullyQualifiedName~ChinookEndpointsTests"
```

Expected: PASS.

**Step 2: Run the full solution verification**

```bash
dotnet test EfUi.sln --no-restore --nologo -v minimal
```

Expected: PASS for the whole solution.

**Step 3: Manual smoke test**

Run the sample host:

```bash
dotnet run --project src/EfUi.SampleHost/EfUi.SampleHost.csproj
```

Then verify manually:
- open `/simple/users/1/edit`
- confirm improved spacing, labels, inputs, and save button styling
- open `/chinook/playlists/1/edit`
- confirm selected tracks appear as chips after page load
- confirm typing narrows available results
- confirm add/remove updates chips immediately
- submit and confirm playlist changes persist

**Step 4: Review diff before final commit**

```bash
git status --short
git diff -- src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.AspNetCore/EfUiFormCss.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
```

Expected: only the intended form-theme and chip-picker changes.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.AspNetCore/EfUiFormCss.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs docs/plans/2026-05-17-forms-chip-picker-implementation.md
git commit -m "feat: polish forms and add chip picker"
```
