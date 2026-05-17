# Many-to-Many Filterable Picker Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Replace the current many-to-many native multi-select with a compact filterable checkbox picker that keeps selected items visible and supports reliable multi-value form posting.

**Architecture:** Keep EF UI server-rendered and progressively enhance only the many-to-many picker with a tiny client-side contains filter. Change the form data contract so collection fields are handled as true multi-value inputs end-to-end instead of being flattened into a comma-separated string. Preserve the existing relationship semantics and supported many-to-many scope.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core metadata/crud layer, HTML rendering in `EfUi.Core`, existing xUnit test projects

---

### Task 1: Lock down the new many-to-many picker rendering

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`

**Step 1: Write the failing renderer test**

Add or update a renderer test in `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs` that asserts a collection field renders:
- a search input
- a boxed checkbox-list container
- checkbox inputs named after the collection field
- checked options for selected related rows
- no `<select multiple>` markup for that field

Use the existing related-option labels already produced today so the test proves the component reuses current display text.

**Step 2: Run the focused renderer test to verify it fails**

Run: `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests`

Expected: FAIL because collection fields still render `<select multiple>` and do not emit filter markup.

**Step 3: Write the minimal rendering implementation**

Update `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` so `EditableFieldKind.Collection` renders:
- a search `<input type="search">`
- a compact scrollable wrapper
- one checkbox row per `RelatedEntityOption`
- inline or local minimal JavaScript that performs case-insensitive contains filtering
- visibility rule: checked rows always stay visible

Keep the control framework-free and progressively enhanced.

If needed, adjust `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs` only to keep the signatures aligned with any submitted-values contract changes from later tasks.

**Step 4: Run the focused renderer test to verify it passes**

Run: `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter HtmlPageRendererTests`

Expected: PASS with the new checkbox picker markup.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Rendering/IHtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: render filterable many-to-many picker"
```

### Task 2: Preserve repeated form values for collection fields

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.Core/Crud/EntityCrudService.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
- Test: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`

**Step 1: Write the failing CRUD test**

Add or update a CRUD test in `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs` that submits a collection field as repeated values rather than a comma-separated string and verifies:
- multiple selected related rows are saved
- an empty selection clears the relationship set

Model the input shape after real checkbox posting semantics.

**Step 2: Run the focused CRUD test to verify it fails**

Run: `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter EntityCrudServiceTests`

Expected: FAIL because collection values are still flattened into a single string contract.

**Step 3: Write the minimal multi-value binding implementation**

Change the submitted-values contract from single string values to multi-value collections where needed.

Update `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs` to:
- preserve all submitted values from `ReadFormAsync()`
- treat missing rendered collection fields as empty selections on submit
- continue supporting scalar/reference fields cleanly

Update `src/EfUi.Core/Crud/EntityCrudService.cs` so collection fields consume repeated values directly instead of splitting comma-separated strings.

Update `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` and `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs` as needed so submitted values can still be reflected back after validation errors.

**Step 4: Run the focused CRUD test to verify it passes**

Run: `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter EntityCrudServiceTests`

Expected: PASS with repeated values supported and empty selection clearing relationships.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Crud/EntityCrudService.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Rendering/IHtmlPageRenderer.cs tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs
git commit -m "feat: support multi-value collection form binding"
```

### Task 3: Verify real ASP.NET Chinook behavior end-to-end

**Files:**
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `README.md`

**Step 1: Write the failing endpoint test**

Update `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs` to assert `/chinook/playlists/1/edit` renders:
- the search input for `Tracks`
- checkbox inputs for `Tracks`
- selected track checkboxes marked checked
- no native multi-select for the many-to-many field

Add or update a POST test that submits repeated `Tracks` values and verifies the playlist is updated correctly. Add a companion test for submitting no `Tracks` values and verifying the relationship set is cleared.

If needed, add a narrower HTML-shape assertion in `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` for shared edit-form behavior.

**Step 2: Run the focused ASP.NET tests to verify they fail**

Run: `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter ChinookEndpointsTests`

Expected: FAIL because the form still renders a multi-select and/or does not honor checkbox-post semantics.

**Step 3: Write the minimal ASP.NET integration/docs changes**

Finish any remaining `EfUiApplicationBuilderExtensions` adjustments so the real HTTP flow matches the CRUD contract and the renderer output.

Update `README.md` to document that supported many-to-many fields now render as a filterable checkbox picker with client-side contains search.

**Step 4: Run the focused ASP.NET tests and broader verification**

Run:
- `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter ChinookEndpointsTests`
- `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
- `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`
- `dotnet test EfUi.sln`

Expected: PASS for all commands.

**Step 5: Manual verification**

Run the sample host and verify in a browser:
- `/chinook/playlists/1/edit` shows a compact filterable checkbox picker for `Tracks`
- typing filters unchecked options by contains match
- checked items remain visible while filtering
- checking/unchecking updates the playlist correctly

Suggested run command:

```bash
dotnet run --project src/EfUi.SampleHost/EfUi.SampleHost.csproj
```

**Step 6: Commit**

```bash
git add tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs README.md src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Crud/EntityCrudService.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Rendering/IHtmlPageRenderer.cs
git commit -m "feat: add searchable many-to-many picker"
```

### Task 4: Final verification and quality pass

**Files:**
- Review: `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.md`
- Review: `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.json`

**Step 1: Run the repo Sonar task**

Run: `mise run sonar`

Expected: Sonar scan completes and refreshes the local summary artifacts.

**Step 2: Inspect the generated local Sonar summaries**

Review:
- `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.md`
- `C:\Users\Administrator\AppData\Local\pi\ef-ui\sonar\summary.json`

Expected: no new security findings and no new unresolved high-impact findings caused by the picker work.

**Step 3: Commit if clean**

If the implementation commit has not already been created after the previous tasks, create the final feature commit here. Otherwise, record the verification outcome and keep the branch ready for the next request.
