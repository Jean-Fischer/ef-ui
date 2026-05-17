# Relationship Support Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add safe, metadata-driven support for principal-side one-to-many editing and simple join-entity-with-payload management while preserving the existing many-to-one and many-to-many behaviors.

**Architecture:** Extend relationship metadata so the system can distinguish many-to-many skip navigations, principal-side one-to-many collections, and collection navigations that should render only as management links. Keep the UI server-rendered, reuse the existing related-entity label heuristics, and enforce all ownership/requiredness rules in the CRUD layer so disabled HTML options are only a convenience, not a security boundary.

**Tech Stack:** C# 12, .NET, ASP.NET Core minimal APIs, Entity Framework Core, xUnit, FluentAssertions, SQLite

---

## Pre-flight notes for the implementer

- Execute this plan in a **dedicated git worktree**.
- Keep the first pass **update-only** for principal-side one-to-many collections when the parent key is store-generated. This avoids trying to attach children before the parent key exists.
- Reuse the current label heuristic (`Name`, `Title`, `Email`, then PK) for both selectable children and “assigned to …” ownership text.
- Prefer adding small focused metadata types over overloading string fields with multiple meanings.
- Do not add nested subforms, async search, or automatic reparenting.

## Task 1: Extend metadata to distinguish one-to-many, many-to-many, and manage-link collections

**Files:**
- Create: `src/EfUi.Core/Metadata/CollectionRelationshipKind.cs`
- Create: `src/EfUi.Core/Metadata/RelatedEntityManagementLink.cs`
- Modify: `src/EfUi.Core/Metadata/EditableFieldMetadata.cs:3-18`
- Modify: `src/EfUi.Core/Metadata/EntityMetadata.cs:3-58`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs:23-96`
- Test: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs:84-108` plus new test helpers at the bottom of the file

**Step 1: Write the failing metadata tests**

Add these tests to `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`.

```csharp
[Fact]
public void GetEntity_exposes_principal_one_to_many_as_update_only_collection_field()
{
    using var db = CreateDb();
    var sut = new EfEntityMetadataProvider();

    var group = sut.GetEntity(db, "groups");
    var users = group.UpdateEditableFields.Single(x => x.Name == "Users");

    users.Kind.Should().Be(EditableFieldKind.Collection);
    users.CollectionRelationshipKind.Should().Be(CollectionRelationshipKind.OneToMany);
    users.ScalarPropertyName.Should().Be("GroupId");
    users.NavigationPropertyName.Should().Be("Users");
    users.RelatedClrType.Should().Be(typeof(User));
    users.IsRequired.Should().BeFalse();

    group.CreateEditableFields.Select(x => x.Name).Should().NotContain("Users");
}

[Fact]
public void GetEntity_keeps_skip_navigation_as_many_to_many_collection_field()
{
    using var db = CreateManyToManyDb();
    var sut = new EfEntityMetadataProvider();

    var playlist = sut.GetEntity(db, "playlists");
    var tracks = playlist.UpdateEditableFields.Single(x => x.Name == "Tracks");

    tracks.Kind.Should().Be(EditableFieldKind.Collection);
    tracks.CollectionRelationshipKind.Should().Be(CollectionRelationshipKind.ManyToMany);
}

[Fact]
public void GetEntity_exposes_payload_join_collection_as_management_link()
{
    using var db = CreatePayloadJoinDb();
    var sut = new EfEntityMetadataProvider();

    var order = sut.GetEntity(db, "orders");

    order.UpdateEditableFields.Select(x => x.Name).Should().NotContain("OrderLines");
    order.RelatedManagementLinks.Should().ContainSingle(x => x.Name == "OrderLines" && x.RouteName == "order_lines");
}
```

Add a small local test DbContext at the bottom of the same file:

```csharp
private static PayloadJoinDbContext CreatePayloadJoinDb()
{
    var options = new DbContextOptionsBuilder<PayloadJoinDbContext>()
        .UseSqlite("Data Source=:memory:")
        .Options;

    var db = new PayloadJoinDbContext(options);
    db.Database.OpenConnection();
    db.Database.EnsureCreated();
    return db;
}

private sealed class PayloadJoinDbContext(DbContextOptions<PayloadJoinDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
}

private sealed class Order
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<OrderLine> OrderLines { get; set; } = [];
}

private sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<OrderLine> OrderLines { get; set; } = [];
}

private sealed class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public decimal UnitPrice { get; set; }
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
```

**Step 2: Run the metadata tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityMetadataProviderTests"
```

Expected: FAIL because `EditableFieldMetadata` and `EntityMetadata` do not yet expose relationship mode / management-link metadata, and `EfEntityMetadataProvider` does not yet detect principal-side one-to-many or payload joins.

**Step 3: Write the minimal metadata implementation**

Create `src/EfUi.Core/Metadata/CollectionRelationshipKind.cs`:

```csharp
namespace EfUi.Core.Metadata;

public enum CollectionRelationshipKind
{
    None,
    ManyToMany,
    OneToMany
}
```

Create `src/EfUi.Core/Metadata/RelatedEntityManagementLink.cs`:

```csharp
namespace EfUi.Core.Metadata;

public sealed record RelatedEntityManagementLink(string Name, string RouteName, Type RelatedClrType);
```

Extend `EditableFieldMetadata` so collection fields can carry more meaning:

```csharp
public sealed record EditableFieldMetadata(
    string Name,
    EditableFieldKind Kind,
    Type ValueType,
    string? ScalarPropertyName,
    string? NavigationPropertyName,
    Type? RelatedClrType,
    bool IsRequired,
    CollectionRelationshipKind CollectionRelationshipKind = CollectionRelationshipKind.None);
```

Extend `EntityMetadata` with a new property and constructor overload default:

```csharp
public IReadOnlyList<RelatedEntityManagementLink> RelatedManagementLinks { get; }
```

In `EfEntityMetadataProvider.Build(...)`:
- keep the existing dependent-side reference-field logic
- keep skip navigations, but tag them with `CollectionRelationshipKind.ManyToMany`
- inspect non-skip collection navigations (`GetNavigations().Where(x => x.IsCollection)`) and classify them as:
  - **one-to-many collection field** when the target entity has exactly one FK and that FK points back to the current entity with a single-column FK
  - **management link** when the target entity points back to the current entity but also has another FK to a different principal (the payload-join case)
- add one-to-many collection fields to `UpdateEditableFields` only for this first pass
- add management links to `EntityMetadata.RelatedManagementLinks`

A minimal one-to-many field shape is:

```csharp
new EditableFieldMetadata(
    navigation.Name,
    EditableFieldKind.Collection,
    typeof(string[]),
    foreignKey.Properties[0].Name,
    navigation.Name,
    navigation.TargetEntityType.ClrType,
    !IsNullable(foreignKey.Properties[0].ClrType),
    CollectionRelationshipKind.OneToMany)
```

**Step 4: Run the metadata tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityMetadataProviderTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Metadata/CollectionRelationshipKind.cs src/EfUi.Core/Metadata/RelatedEntityManagementLink.cs src/EfUi.Core/Metadata/EditableFieldMetadata.cs src/EfUi.Core/Metadata/EntityMetadata.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs
git commit -m "feat: classify relationship collection metadata"
```

### Task 2: Render disabled one-to-many options and payload-join manage links

**Files:**
- Modify: `src/EfUi.Core/Rendering/RelatedEntityOption.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs:64-188`
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs:114-200` and helper methods near the bottom
- Modify: `src/EfUi.Core/Metadata/EntityMetadata.cs` if constructor helpers need an overload for related-management links

**Step 1: Write the failing renderer tests**

Add a one-to-many rendering test:

```csharp
[Fact]
public void RenderEditForm_renders_one_to_many_picker_with_disabled_assigned_elsewhere_options()
{
    var sut = new HtmlPageRenderer();
    var metadata = new EntityMetadata(
        "Group",
        "groups",
        typeof(TenantRow),
        PrimaryKey("Id", typeof(int)),
        new[]
        {
            PrimaryKey("Id", typeof(int)),
            Editable("Name", typeof(string))
        },
        new[]
        {
            Editable("Name", typeof(string))
        },
        new[]
        {
            ScalarField("Name", typeof(string))
        },
        new[]
        {
            ScalarField("Name", typeof(string)),
            CollectionField("Users", typeof(TenantRow), CollectionRelationshipKind.OneToMany)
        });

    var html = sut.RenderEditForm(
        "/efui",
        metadata,
        new TenantRow { Name = "Admins" },
        isCreate: false,
        errors: new Dictionary<string, string[]>(),
        key: 1,
        fieldOptions: new Dictionary<string, IReadOnlyList<RelatedEntityOption>>
        {
            ["Users"] =
            [
                new RelatedEntityOption("1", "Ada", Selected: true),
                new RelatedEntityOption("2", "Linus", Selected: false, Disabled: true, Description: "assigned to Guests")
            ]
        });

    html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"1\" checked");
    html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"2\" disabled");
    html.Should().Contain("assigned to Guests");
}
```

Add a manage-link rendering test:

```csharp
[Fact]
public void RenderEditForm_renders_related_management_links_below_editable_fields()
{
    var sut = new HtmlPageRenderer();
    var metadata = new EntityMetadata(
        "Invoice",
        "invoices",
        typeof(TenantRow),
        PrimaryKey("Id", typeof(int)),
        new[]
        {
            PrimaryKey("Id", typeof(int)),
            Editable("Name", typeof(string))
        },
        new[]
        {
            Editable("Name", typeof(string))
        },
        new[]
        {
            ScalarField("Name", typeof(string))
        },
        new[]
        {
            ScalarField("Name", typeof(string))
        },
        relatedManagementLinks:
        [
            new RelatedEntityManagementLink("InvoiceItems", "invoice_items", typeof(TenantRow))
        ]);

    var html = sut.RenderEditForm(
        "/efui",
        metadata,
        new TenantRow { Name = "Invoice 1" },
        isCreate: false,
        errors: new Dictionary<string, string[]>(),
        key: 1);

    html.Should().Contain("Manage related rows");
    html.Should().Contain("/efui/invoice_items");
    html.Should().NotContain("name=\"InvoiceItems\" type=\"checkbox\"");
}
```

Update the existing helper method to carry collection mode:

```csharp
private static EditableFieldMetadata CollectionField(string name, Type relatedClrType, CollectionRelationshipKind relationshipKind)
    => new(name, EditableFieldKind.Collection, typeof(string[]), null, name, relatedClrType, false, relationshipKind);
```

**Step 2: Run the renderer tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~HtmlPageRendererTests"
```

Expected: FAIL because `RelatedEntityOption` cannot represent disabled/description state and `HtmlPageRenderer` cannot render management links.

**Step 3: Write the minimal renderer implementation**

Extend `RelatedEntityOption`:

```csharp
namespace EfUi.Core.Rendering;

public sealed record RelatedEntityOption(
    string Value,
    string Label,
    bool Selected = false,
    bool Disabled = false,
    string? Description = null);
```

In `HtmlPageRenderer.RenderEditForm(...)`:
- after rendering editable fields, render a simple related-management section when `!isCreate && entity.RelatedManagementLinks.Any()`
- keep the collection picker script shared for many-to-many and one-to-many collections

Update `RenderCollectionField(...)` so each option row can emit disabled state and secondary text:

```csharp
var disabled = option.Disabled ? " disabled" : string.Empty;
var description = string.IsNullOrWhiteSpace(option.Description)
    ? string.Empty
    : $" <small>{WebUtility.HtmlEncode(option.Description)}</small>";

html.Append($"<input name=\"{fieldName}\" type=\"checkbox\" value=\"{encodedValue}\"{selected}{disabled} /> <span>{encodedLabel}</span>{description}");
```

Add a helper for related-management links, for example:

```csharp
private static void RenderRelatedManagementLinks(StringBuilder html, string routePrefix, EntityMetadata entity)
{
    html.Append("<section><h2>Related rows</h2>");
    foreach (var link in entity.RelatedManagementLinks)
    {
        html.Append($"<div><label>{WebUtility.HtmlEncode(link.Name)}</label> <a href=\"{routePrefix}/{link.RouteName}\">Manage related rows</a></div>");
    }
    html.Append("</section>");
}
```

**Step 4: Run the renderer tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~HtmlPageRendererTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Rendering/RelatedEntityOption.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Metadata/EntityMetadata.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs
git commit -m "feat: render one-to-many disabled states and manage links"
```

### Task 3: Lock down one-to-many CRUD rules with failing tests

**Files:**
- Modify: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs:63-260` plus new helper DbContext types near the bottom
- Reuse: `tests/EfUi.Core.Tests/TestDoubles/SampleModelDbContext.cs`
- Modify later: `src/EfUi.Core/Crud/EntityCrudService.cs:103-158`

**Step 1: Write the failing CRUD tests**

Add an optional one-to-many attach/detach test using the existing `SampleModelDbContext` shape:

```csharp
[Fact]
public async Task UpdateAsync_reconciles_optional_one_to_many_collection_from_group_side()
{
    await using var db = await CreateDbAsync();

    db.Users.Add(new User
    {
        Name = "Unassigned",
        Email = "unassigned@example.com",
        IsActive = true,
        CreatedAt = new DateTime(2026, 5, 17)
    });
    await db.SaveChangesAsync();

    var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

    var result = await sut.UpdateAsync(db, "groups", 1, new Dictionary<string, string[]>
    {
        ["Name"] = ["Admins"],
        ["Users"] = ["3"]
    });

    result.IsSuccess.Should().BeTrue();

    var users = await db.Users.OrderBy(x => x.Id).ToListAsync();
    users.Single(x => x.Id == 1).GroupId.Should().BeNull();
    users.Single(x => x.Id == 3).GroupId.Should().Be(1);
}
```

Add a tampering test:

```csharp
[Fact]
public async Task UpdateAsync_rejects_one_to_many_selection_for_child_owned_by_another_parent()
{
    await using var db = await CreateDbAsync();
    var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

    var result = await sut.UpdateAsync(db, "groups", 1, new Dictionary<string, string[]>
    {
        ["Name"] = ["Admins"],
        ["Users"] = ["1", "2"]
    });

    result.IsSuccess.Should().BeFalse();
    result.Errors.Should().ContainKey("Users");

    var users = await db.Users.OrderBy(x => x.Id).ToListAsync();
    users.Single(x => x.Id == 1).GroupId.Should().Be(1);
    users.Single(x => x.Id == 2).GroupId.Should().Be(2);
}
```

Add a required-FK removal test with a local DbContext inside the same file:

```csharp
[Fact]
public async Task UpdateAsync_rejects_required_one_to_many_removal()
{
    await using var db = await CreateRequiredOneToManyDbAsync();
    var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

    var result = await sut.UpdateAsync(db, "artists", 1, new Dictionary<string, string[]>
    {
        ["Name"] = ["Queen"],
        ["Albums"] = []
    });

    result.IsSuccess.Should().BeFalse();
    result.Errors.Should().ContainKey("Albums");
}
```

Add a tiny local required-FK model:

```csharp
private static async Task<RequiredOneToManyDbContext> CreateRequiredOneToManyDbAsync()
{
    var options = new DbContextOptionsBuilder<RequiredOneToManyDbContext>()
        .UseSqlite("Data Source=:memory:")
        .Options;

    var db = new RequiredOneToManyDbContext(options);
    await db.Database.OpenConnectionAsync();
    await db.Database.EnsureCreatedAsync();

    db.Artists.Add(new RequiredArtist
    {
        Name = "Queen",
        Albums = [new RequiredAlbum { Title = "News of the World" }]
    });
    await db.SaveChangesAsync();
    return db;
}
```

**Step 2: Run the CRUD tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityCrudServiceTests"
```

Expected: FAIL because collection updates currently assume many-to-many semantics only.

**Step 3: Write the minimal CRUD implementation**

In `src/EfUi.Core/Crud/EntityCrudService.cs`:
- change `ApplyValuesAsync(...)` so it has access to the parent `EntityMetadata` or at least the parent key/property info
- split collection handling into two code paths:
  - `CollectionRelationshipKind.ManyToMany`
  - `CollectionRelationshipKind.OneToMany`

A minimal one-to-many reconciler should follow this shape:

```csharp
private async Task<CrudOperationResult> ApplyOneToManyCollectionFieldAsync(
    DbContext dbContext,
    object parent,
    EntityMetadata entity,
    EditableFieldMetadata field,
    IReadOnlyList<string> rawValues)
{
    var parentKey = parent.GetType().GetProperty(entity.PrimaryKeyProperty.Name)!.GetValue(parent)!;
    var selectedIds = rawValues.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
    var childEntityType = dbContext.Model.FindEntityType(field.RelatedClrType!)!;
    var childKey = childEntityType.FindPrimaryKey()!.Properties.Single();
    var childFk = childEntityType.FindProperty(field.ScalarPropertyName!)!;

    var currentChildren = await LoadCurrentChildrenAsync(dbContext, parent, field);
    var allChildren = ReadRows(dbContext, field.RelatedClrType!).ToList();

    foreach (var child in allChildren)
    {
        var childId = FormatValue(child.GetType().GetProperty(childKey.Name)!.GetValue(child));
        var currentOwner = child.GetType().GetProperty(childFk.Name)!.GetValue(child);

        if (selectedIds.Contains(childId))
        {
            if (currentOwner is not null && !Equals(currentOwner, parentKey))
            {
                return CrudOperationResult.Failure(field.Name, "Selected related row is already assigned to another parent.");
            }

            child.GetType().GetProperty(childFk.Name)!.SetValue(child, parentKey);
        }
    }

    foreach (var child in currentChildren.Where(child => !selectedIds.Contains(GetChildId(child))))
    {
        if (field.IsRequired)
        {
            return CrudOperationResult.Failure(field.Name, "Required related rows cannot be removed without reassignment.");
        }

        child.GetType().GetProperty(childFk.Name)!.SetValue(child, null);
    }

    return CrudOperationResult.Success();
}
```

Keep the existing many-to-many path unchanged except for routing through the new relationship-kind switch.

**Step 4: Run the CRUD tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityCrudServiceTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Crud/EntityCrudService.cs tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs
git commit -m "feat: enforce one-to-many CRUD rules"
```

### Task 4: Build one-to-many option state and ownership labels in ASP.NET Core

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:92-126`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:262-371`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs:57-118`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs:38-79`

**Step 1: Write the failing endpoint tests**

Add a simple-db one-to-many rendering test:

```csharp
[Fact]
public async Task Get_group_edit_form_renders_users_as_one_to_many_picker_with_disabled_foreign_owned_rows()
{
    var html = await _client.GetStringAsync("/simple/groups/1/edit");

    html.Should().Contain("type=\"search\"");
    html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"1\" checked");
    html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"2\" disabled");
    html.Should().Contain("assigned to Guests");
    html.Should().NotContain("name=\"GroupId\"");
}
```

Add an optional detach endpoint test:

```csharp
[Fact]
public async Task Post_update_group_with_no_users_clears_optional_one_to_many_assignments()
{
    var response = await _client.PostAsync(
        "/simple/groups/1",
        new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Name", "Admins")
        }));

    response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);

    var html = await _client.GetStringAsync("/simple/groups/1/edit");
    html.Should().NotContain("name=\"Users\" type=\"checkbox\" value=\"1\" checked");
}
```

Add a required-FK endpoint test in Chinook:

```csharp
[Fact]
public async Task Post_update_artist_with_no_albums_returns_required_relationship_validation_error()
{
    var response = await _client.PostAsync(
        "/chinook/artists/1",
        new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Name", "AC/DC")
        }));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var html = await response.Content.ReadAsStringAsync();
    html.Should().Contain("Albums");
    html.Should().Contain("cannot be removed");
}
```

Add a payload-join manage-link test using Chinook `Invoice -> InvoiceItems`:

```csharp
[Fact]
public async Task Get_invoice_edit_form_shows_manage_link_for_payload_join_rows()
{
    var html = await _client.GetStringAsync("/chinook/invoices/1/edit");

    html.Should().Contain("Manage related rows");
    html.Should().Contain("/chinook/invoice_items");
    html.Should().NotContain("name=\"InvoiceItems\" type=\"checkbox\"");
}
```

**Step 2: Run the endpoint tests to verify they fail**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests|FullyQualifiedName~ChinookEndpointsTests"
```

Expected: FAIL because field-option building still assumes only reference or many-to-many collection semantics.

**Step 3: Write the minimal ASP.NET Core implementation**

In `BuildFieldOptions(...)`:
- keep reference dropdown behavior unchanged
- for `CollectionRelationshipKind.ManyToMany`, keep the current selection logic
- for `CollectionRelationshipKind.OneToMany`, compute each option as one of:
  - selected for this parent
  - enabled and unassigned
  - disabled and owned by another parent

Create a helper that builds the ownership description:

```csharp
private static string? GetOwnershipDescription(DbContext dbContext, EditableFieldMetadata field, object child, object currentParentKey)
{
    var ownerKey = child.GetType().GetProperty(field.ScalarPropertyName!)?.GetValue(child);
    if (ownerKey is null || Equals(ownerKey, currentParentKey))
    {
        return null;
    }

    var owner = dbContext.Find(dbContext.Model.FindEntityType(field.NavigationPropertyName!)?.ClrType ?? throw new InvalidOperationException(), ownerKey);
    return owner is null ? "assigned elsewhere" : $"assigned to {GetRelatedEntityLabel(owner, string.Empty, FormatValue(ownerKey))}";
}
```

Do **not** keep that exact broken `field.NavigationPropertyName` lookup. Instead, use the current entity metadata / principal CLR type to load the owning parent row by key. The point of the step is: compute an ownership description from the current FK owner when it is not the row being edited.

Update `GetSelectedValues(...)` so one-to-many selection is based on the child FK rather than a loaded collection only.

Keep `EnsureCollectionFieldsPresent(...)` for update forms so an omitted one-to-many field still means “selected set is empty”.

Also pass `entity.RelatedManagementLinks` through the edit-form render path.

**Step 4: Run the endpoint tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests|FullyQualifiedName~ChinookEndpointsTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs
git commit -m "feat: surface one-to-many states in efui forms"
```

### Task 5: Update docs and add the missing create-form guardrail test

**Files:**
- Modify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `README.md:19-20`

**Step 1: Write the failing guardrail/docs tests**

Add an endpoint test that create-form one-to-many does not appear for generated-key parents:

```csharp
[Fact]
public async Task Get_group_create_form_does_not_render_one_to_many_picker_before_parent_exists()
{
    var html = await _client.GetStringAsync("/simple/groups/new");

    html.Should().NotContain("name=\"Users\" type=\"checkbox\"");
}
```

**Step 2: Run the targeted tests to verify the guardrail is covered**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityMetadataProviderTests" && dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests.Get_group_create_form_does_not_render_one_to_many_picker_before_parent_exists"
```

Expected: first command PASS if Task 1 already enforced update-only one-to-many fields; second command may FAIL if the create page still renders the picker.

**Step 3: Write the minimal documentation and guardrail implementation**

If the endpoint test fails, fix it by ensuring `CreateEditableFields` never includes principal-side one-to-many collection fields for generated-key parents.

Then update `README.md` form behavior bullets to say:

```md
- many-to-one relationships render as dropdowns
- supported many-to-many skip navigations render as a filterable checkbox picker with client-side contains search
- supported one-to-many relationships render on edit forms as a filterable checkbox picker, with rows already assigned elsewhere shown disabled
- join entities with payload are managed through related-row links instead of inline nested editors
```

**Step 4: Run the targeted tests to verify they pass**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~Get_group_create_form_does_not_render_one_to_many_picker_before_parent_exists"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs README.md
git commit -m "docs: describe one-to-many and payload join behavior"
```

### Task 6: Full verification and cleanup

**Files:**
- Review: `src/EfUi.Core/Metadata/EditableFieldMetadata.cs`
- Review: `src/EfUi.Core/Metadata/EntityMetadata.cs`
- Review: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- Review: `src/EfUi.Core/Crud/EntityCrudService.cs`
- Review: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Review: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Review: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Review: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Review: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`
- Review: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Review: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Review: `README.md`

**Step 1: Run the focused test suites**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityMetadataProviderTests|FullyQualifiedName~HtmlPageRendererTests|FullyQualifiedName~EntityCrudServiceTests"
```

Expected: PASS.

**Step 2: Run the ASP.NET Core test suite**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: PASS.

**Step 3: Run the full solution tests**

Run:

```bash
dotnet test EfUi.sln
```

Expected: PASS with no newly failing tests.

**Step 4: Review the diff for scope control**

Run:

```bash
git diff --stat HEAD~4..HEAD
git diff
```

Expected: only relationship metadata, renderer, CRUD, endpoint tests, and README changes; no unrelated refactors.

**Step 5: Commit the final polish if needed**

```bash
git add src/EfUi.Core/Metadata/EditableFieldMetadata.cs src/EfUi.Core/Metadata/EntityMetadata.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs src/EfUi.Core/Crud/EntityCrudService.cs src/EfUi.Core/Rendering/HtmlPageRenderer.cs src/EfUi.Core/Rendering/RelatedEntityOption.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs README.md
git commit -m "test: finalize relationship editing coverage"
```

## Notes for the executing engineer

- If the current metadata shape becomes awkward, prefer adding a small focused metadata type rather than stuffing more overloaded meaning into `EditableFieldMetadata` string properties.
- When building one-to-many ownership descriptions, use the same friendly-label heuristic already used for related options.
- Keep server-side validation authoritative. Disabled checkboxes are informational only.
- If a payload-join filtered link (for example `/chinook/invoice_items?invoiceId=1`) is cheap to add safely, do it. If not, ship the unfiltered `Manage related rows` link first.
- Do not broaden the scope to one-to-one or nested payload editing in this pass.
