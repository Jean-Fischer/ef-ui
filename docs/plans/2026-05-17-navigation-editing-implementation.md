# Navigation Editing Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add navigation-aware editing to EF UI so create/edit forms handle primary keys correctly, render many-to-one relationships as dropdowns, and support simple skip-navigation many-to-many relationships with multi-select controls.

**Architecture:** Keep EF UI metadata-driven, but introduce explicit editable form-field metadata instead of relying only on scalar properties. Build the first version around three field kinds—scalar, reference, and collection—while keeping persistence through EF Core, using FK scalar updates for many-to-one and collection reconciliation for supported many-to-many skip navigations.

**Tech Stack:** .NET 8, EF Core 8, ASP.NET Core, xUnit, FluentAssertions, SQLite

---

### Task 1: Lock down primary-key form behavior with failing renderer tests

**Files:**
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Inspect: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Inspect: `src/EfUi.Core/Metadata/EntityMetadata.cs`

**Step 1: Write the failing test for generated keys on edit**

Add a test asserting that edit forms display the PK value read-only instead of omitting it entirely.

```csharp
[Fact]
public void RenderEditForm_shows_generated_primary_key_as_read_only()
{
    var sut = new HtmlPageRenderer();
    var metadata = new EntityMetadata(
        "User",
        "users",
        typeof(UserRow),
        PrimaryKey("Id", typeof(int)),
        new[]
        {
            PrimaryKey("Id", typeof(int)),
            Editable("Name", typeof(string))
        },
        new[]
        {
            Editable("Name", typeof(string))
        });

    var html = sut.RenderEditForm(
        "/efui",
        metadata,
        new UserRow { Id = 7, Name = "Ada" },
        isCreate: false,
        errors: new Dictionary<string, string[]>(),
        key: 7);

    html.Should().Contain("Id");
    html.Should().Contain(">7<");
    html.Should().NotContain("name=\"Id\"");
}
```

**Step 2: Update the existing assigned-key test to the new rule**

Replace the current expectation that assigned PKs are editable on create and hidden on update with:
- editable on create
- shown read-only on edit
- not editable on edit

**Step 3: Run the focused renderer tests to verify failure**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "RenderEditForm_shows_generated_primary_key_as_read_only|RenderForm_includes_assigned_primary_key_on_create_but_not_on_update"
```

Expected: FAIL because the renderer currently omits the PK on edit entirely.

**Step 4: Implement the minimal renderer change**

Update `HtmlPageRenderer.RenderEditForm(...)` so edit forms render a small read-only PK block before editable fields.

Example shape:

```csharp
if (!isCreate)
{
    var keyValue = model is null
        ? FormatValue(key)
        : FormatValue(model.GetType().GetProperty(entity.PrimaryKeyProperty.Name)?.GetValue(model));

    html.Append($"<div><label>{WebUtility.HtmlEncode(entity.PrimaryKeyProperty.Name)}</label><span>{WebUtility.HtmlEncode(keyValue)}</span></div>");
}
```

Do not add a writable `<input name="Id" ...>`.

**Step 5: Run the focused renderer tests again**

Run the same command.

Expected: PASS.

**Step 6: Commit checkpoint**

```bash
git add tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs
git commit -m "test: lock down primary key form behavior"
```

### Task 2: Add explicit editable field metadata for scalar and reference fields

**Files:**
- Create: `src/EfUi.Core/Metadata/EditableFieldMetadata.cs`
- Modify: `src/EfUi.Core/Metadata/EntityMetadata.cs`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Modify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`

**Step 1: Write the failing metadata test for many-to-one reference fields**

Add a test using the sample test double model that asserts `User -> Group` is exposed as a reference field rather than just a raw scalar FK field.

Example shape:

```csharp
[Fact]
public void GetEntity_exposes_many_to_one_navigation_as_reference_field()
{
    using var db = CreateDb();
    var sut = new EfEntityMetadataProvider();

    var user = sut.GetEntity(db, "users");

    user.EditableFields.Select(x => x.Name).Should().Contain("Group");
    user.EditableFields.Select(x => x.Name).Should().NotContain("GroupId");
}
```

**Step 2: Define editable field metadata**

Create a small metadata type with enough information for rendering and binding.

Minimum fields:

```csharp
public enum EditableFieldKind
{
    Scalar,
    Reference,
    Collection
}

public sealed record EditableFieldMetadata(
    string Name,
    EditableFieldKind Kind,
    Type ValueType,
    string? ScalarPropertyName,
    string? NavigationPropertyName,
    Type? RelatedClrType,
    bool IsRequired);
```

Keep it minimal; do not add speculative configuration.

**Step 3: Extend `EntityMetadata`**

Add:
- `CreateEditableFields`
- `UpdateEditableFields`

Keep the existing property metadata for lists/tables if still needed elsewhere.

**Step 4: Implement many-to-one field detection in `EfEntityMetadataProvider`**

For FK-backed reference navigations:
- emit one `Reference` field named after the navigation (`Group`)
- suppress the standalone FK scalar field (`GroupId`) from the editable-field list
- continue exposing plain scalar fields as `Scalar`

Use EF relationship metadata rather than string matching.

**Step 5: Run the focused metadata tests**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "GetEntity_exposes_many_to_one_navigation_as_reference_field|Editable_properties_exclude_key_and_navigation_properties"
```

Expected: PASS after the metadata changes.

**Step 6: Commit checkpoint**

```bash
git add src/EfUi.Core/Metadata/EditableFieldMetadata.cs src/EfUi.Core/Metadata/EntityMetadata.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs
git commit -m "feat: add editable field metadata for reference fields"
```

### Task 3: Render many-to-one dropdowns with heuristic labels

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Create: `src/EfUi.Core/Rendering/RelatedEntityOption.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`

**Step 1: Write the failing renderer test for reference dropdowns**

Add a test that renders a form with a `Reference` field and expects a `<select>` instead of a raw text input.

Example shape:

```csharp
[Fact]
public void RenderEditForm_renders_reference_fields_as_dropdowns()
{
    // build metadata with a reference field named Group
    // pass options like (1, "Admins"), (2, "Guests")
    // assert html contains <select name="Group"> and option text "Admins"
}
```

**Step 2: Add a tiny option model**

Create:

```csharp
public sealed record RelatedEntityOption(string Value, string Label, bool Selected = false);
```

**Step 3: Extend renderer API minimally**

Update `RenderEditForm(...)` and `RenderForm(...)` to accept per-field option data.

Do not redesign the whole renderer; pass a dictionary keyed by field name.

**Step 4: Implement label heuristic**

For the first version, use this priority order when building option labels:
1. `Name`
2. `Title`
3. `Email`
4. primary key text

Keep the heuristic in one helper method so it is easy to replace later.

**Step 5: Render reference fields as `<select>`**

If a field is `Reference`, render:

```html
<label>Group</label>
<select name="Group">
  <option value=""></option>
  <option value="1">Admins</option>
</select>
```

Use the selected value from submitted values first, otherwise from the current model FK.

**Step 6: Run the focused renderer tests**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter RenderEditForm_renders_reference_fields_as_dropdowns
```

Expected: PASS.

**Step 7: Commit checkpoint**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Rendering/RelatedEntityOption.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: render many-to-one fields as dropdowns"
```

### Task 4: Bind and persist many-to-one selections through FK columns

**Files:**
- Modify: `src/EfUi.Core/Crud/EntityCrudService.cs`
- Modify: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`
- Inspect: `src/EfUi.Core/Binding/ScalarValueBinder.cs`

**Step 1: Write the failing CRUD test for navigation selection**

Add a test proving a posted `Group` selection updates `GroupId`.

Example shape:

```csharp
[Fact]
public async Task UpdateAsync_updates_many_to_one_navigation_via_reference_field()
{
    await using var db = await CreateDbAsync();
    db.Groups.Add(new Group { Name = "Guests" });
    db.Users.Add(new User { Name = "Ada", Email = "ada@example.com", GroupId = 1, CreatedAt = new DateTime(2026, 5, 17) });
    await db.SaveChangesAsync();

    var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());
    var id = db.Users.Single().Id;

    var result = await sut.UpdateAsync(db, "users", id, new Dictionary<string, string?>
    {
        ["Group"] = "2"
    });

    result.IsSuccess.Should().BeTrue();
    (await db.Users.SingleAsync()).GroupId.Should().Be(2);
}
```

**Step 2: Run the focused CRUD test to verify failure**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter UpdateAsync_updates_many_to_one_navigation_via_reference_field
```

Expected: FAIL because `EntityCrudService` only applies scalar-property posts right now.

**Step 3: Extend `EntityCrudService` to use editable fields**

For reference fields:
- read the posted value by field name (`Group`)
- bind it to the FK scalar CLR type (`GroupId` type)
- assign the bound value to the FK property, not to the navigation property

Keep the implementation small: do not attach related entities directly.

**Step 4: Run the focused CRUD test again**

Run the same command.

Expected: PASS.

**Step 5: Commit checkpoint**

```bash
git add src/EfUi.Core/Crud/EntityCrudService.cs tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs
git commit -m "feat: persist reference selections through foreign keys"
```

### Task 5: Add simple many-to-many metadata for supported skip navigations

**Files:**
- Modify: `src/EfUi.Core/Metadata/EditableFieldMetadata.cs`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Modify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Possibly inspect: `src/EfUi.SampleHost/Chinook/Playlist.cs`, `src/EfUi.SampleHost/Chinook/Track.cs`

**Step 1: Write the failing metadata test for supported many-to-many fields**

Use the existing in-memory many-to-many test context and assert a collection field is exposed.

Example shape:

```csharp
[Fact]
public void GetEntity_exposes_supported_skip_navigation_as_collection_field()
{
    using var db = CreateManyToManyDb();
    var sut = new EfEntityMetadataProvider();

    var playlist = sut.GetEntity(db, "playlists");

    playlist.UpdateEditableFields.Select(x => x.Name).Should().Contain("Tracks");
}
```

**Step 2: Extend field metadata with collection details**

Add only what the many-to-many binder/renderer needs, such as:
- related entity CLR type
- related key property name
- navigation property name
- whether multiple values are allowed

**Step 3: Detect supported skip navigations**

Only emit a `Collection` field when all conditions hold:
- skip navigation
- collection navigation
- single-column PK on both sides

Do not support explicit join entities in this first version.

**Step 4: Run the focused metadata test**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter GetEntity_exposes_supported_skip_navigation_as_collection_field
```

Expected: PASS.

**Step 5: Commit checkpoint**

```bash
git add src/EfUi.Core/Metadata/EditableFieldMetadata.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs
git commit -m "feat: detect supported many-to-many editable fields"
```

### Task 6: Render many-to-many fields as multi-select controls

**Files:**
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`

**Step 1: Write the failing renderer test for collection fields**

Add a test asserting that a supported collection field renders as a multi-select.

Example shape:

```csharp
[Fact]
public void RenderEditForm_renders_collection_fields_as_multi_selects()
{
    // metadata with collection field Tracks
    // options (1, "Track A", selected), (2, "Track B")
    // assert <select multiple name="Tracks"> exists
}
```

**Step 2: Implement multi-select rendering**

Render:

```html
<label>Tracks</label>
<select name="Tracks" multiple>
  <option value="1" selected>Track A</option>
  <option value="2">Track B</option>
</select>
```

Submitted values should win over model-derived selections.

**Step 3: Run the focused renderer test**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter RenderEditForm_renders_collection_fields_as_multi_selects
```

Expected: PASS.

**Step 4: Commit checkpoint**

```bash
git add src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: render many-to-many fields as multi-selects"
```

### Task 7: Bind and persist many-to-many skip-navigation updates

**Files:**
- Modify: `src/EfUi.Core/Crud/EntityCrudService.cs`
- Modify: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`
- Possibly create sample-only test entities if needed in: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`

**Step 1: Write the failing CRUD test for collection reconciliation**

Use a simple many-to-many test model and verify selected related keys replace the existing collection.

Example shape:

```csharp
[Fact]
public async Task UpdateAsync_reconciles_supported_many_to_many_collection()
{
    // seed playlist with track 1
    // submit Tracks = [2,3]
    // assert playlist now relates to tracks 2 and 3 only
}
```

**Step 2: Run the focused CRUD test to verify failure**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter UpdateAsync_reconciles_supported_many_to_many_collection
```

Expected: FAIL because collection navigation updates are not implemented.

**Step 3: Implement collection reconciliation**

In `EntityCrudService.UpdateAsync(...)`:
- load the entity
- for each collection field, resolve selected keys to related entities
- clear existing collection
- add the resolved related entities

Use EF metadata and reflection conservatively.

Do not implement create-time many-to-many support yet unless the failing tests require it.

**Step 4: Run the focused CRUD test again**

Run the same command.

Expected: PASS.

**Step 5: Commit checkpoint**

```bash
git add src/EfUi.Core/Crud/EntityCrudService.cs tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs
git commit -m "feat: persist supported many-to-many selections"
```

### Task 8: Add end-to-end sample-host coverage for relationship editing

**Files:**
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Possibly modify sample model files if the current sample database lacks a clean many-to-many shape:
  - `src/EfUi.SampleHost/Models/*.cs`
  - `src/EfUi.SampleHost/Data/SampleDbContext.cs`
  - `src/EfUi.SampleHost/Data/SampleDbSeeder.cs`

**Step 1: Write the failing many-to-one endpoint test**

Update the sample-host endpoint tests to assert:
- edit form shows `Group` dropdown
- raw `GroupId` input is not rendered as a plain text input
- updating the dropdown persists the relationship change

**Step 2: Write the failing many-to-many endpoint test**

Prefer Chinook if its `Playlist <-> Track` skip navigation works cleanly; otherwise add a small many-to-many relation to the sample model specifically for test coverage.

Assert:
- edit form shows a multi-select
- posting new selected values updates the relationships

**Step 3: Run focused AspNetCore tests to verify failure**

Run a filter covering the new relationship tests.

Expected: FAIL.

**Step 4: Implement the minimal host glue**

Update the ASP.NET Core layer so form rendering gets relationship options and submitted values are passed through correctly.

This may require small changes in:
- `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`

Do not redesign the endpoint layer; just provide the renderer and CRUD service what they now need.

**Step 5: Run focused AspNetCore relationship tests again**

Expected: PASS.

**Step 6: Commit checkpoint**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs src/EfUi.SampleHost/Models src/EfUi.SampleHost/Data
git commit -m "feat: add relationship editing to sample host"
```

### Task 9: Full verification and documentation

**Files:**
- Modify: `README.md`
- Verify only: `src/EfUi.Core/*`
- Verify only: `src/EfUi.AspNetCore/*`
- Verify only: `src/EfUi.SampleHost/*`

**Step 1: Update README briefly**

Document the new form behavior concisely:
- PK create/edit rules
- relationship editing support
- `/simple` and `/chinook` remain the demo surfaces

**Step 2: Run core tests**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj
```

Expected: PASS.

**Step 3: Run AspNetCore tests**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: PASS.

**Step 4: Run full solution tests**

Run:

```bash
dotnet test EfUi.sln
```

Expected: PASS.

**Step 5: Manual smoke test**

Run the sample host and verify:
- `/simple/users/<id>/edit` shows PK read-only and `Group` dropdown
- the many-to-many demo form shows a multi-select
- `/chinook` relationship editing works for at least one supported entity

**Step 6: Optional Sonar verification**

Run:

```bash
mise run sonar
```

Expected: PASS with no new high-impact findings.

**Step 7: Final commit**

```bash
git add README.md src/EfUi.Core src/EfUi.AspNetCore src/EfUi.SampleHost tests
git commit -m "feat: add navigation-aware relationship editing"
```
