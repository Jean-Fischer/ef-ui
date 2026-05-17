# EF UI PoC Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Build a TDD-first proof of concept for a pluggable ASP.NET Core UI that discovers EF Core entities and provides scalar CRUD through a smooth `UseEfUi(...)` integration surface.

**Architecture:** Keep all meaningful behavior in a reusable core library (`EfUi.Core`) and make the ASP.NET package (`EfUi.AspNetCore`) a thin HTTP adapter. Prove the design with a minimal SQLite sample host first, then keep Chinook as an optional follow-up validation host once the primary CRUD slice is stable.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, EF Core, SQLite, xUnit, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing, HTMX (CDN only)

---

## Before You Start

Read these first:
- `docs/plans/2026-05-17-ef-ui-poc-design.md`
- `doc/poc-design-doc.md`

Use a dedicated worktree if available.

Prefer `net8.0` for all projects even though newer SDKs are installed.

Proposed solution layout:

```text
EfUi.sln
Directory.Build.props
src/
  EfUi.Core/
  EfUi.AspNetCore/
  EfUi.SampleHost/
tests/
  EfUi.Core.Tests/
  EfUi.AspNetCore.Tests/
```

## Task 1: Scaffold the solution and projects

**Files:**
- Create: `EfUi.sln`
- Create: `Directory.Build.props`
- Create: `src/EfUi.Core/EfUi.Core.csproj`
- Create: `src/EfUi.AspNetCore/EfUi.AspNetCore.csproj`
- Create: `src/EfUi.SampleHost/EfUi.SampleHost.csproj`
- Create: `tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
- Create: `tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`

**Step 1: Create the solution shell**

Run:
```bash
dotnet new sln -n EfUi
mkdir -p src/EfUi.Core src/EfUi.AspNetCore src/EfUi.SampleHost tests/EfUi.Core.Tests tests/EfUi.AspNetCore.Tests
```

Expected: solution file and empty project folders exist.

**Step 2: Create `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

**Step 3: Create the project files**

`src/EfUi.Core/EfUi.Core.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
  </ItemGroup>
</Project>
```

`src/EfUi.AspNetCore/EfUi.AspNetCore.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\EfUi.Core\EfUi.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

`src/EfUi.SampleHost/EfUi.SampleHost.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\EfUi.AspNetCore\EfUi.AspNetCore.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
  </ItemGroup>
</Project>
```

`tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\EfUi.Core\EfUi.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  </ItemGroup>
</Project>
```

`tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\EfUi.SampleHost\EfUi.SampleHost.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
  </ItemGroup>
</Project>
```

**Step 4: Add projects to the solution**

Run:
```bash
dotnet sln EfUi.sln add src/EfUi.Core/EfUi.Core.csproj src/EfUi.AspNetCore/EfUi.AspNetCore.csproj src/EfUi.SampleHost/EfUi.SampleHost.csproj tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: all five projects added.

**Step 5: Run restore**

Run:
```bash
dotnet restore EfUi.sln
```

Expected: restore succeeds.

**Step 6: Commit**

```bash
git add EfUi.sln Directory.Build.props src tests
git commit -m "chore: scaffold ef ui solution"
```

## Task 2: Add the sample model and DbContext

**Files:**
- Create: `src/EfUi.SampleHost/Models/User.cs`
- Create: `src/EfUi.SampleHost/Models/Group.cs`
- Create: `src/EfUi.SampleHost/Data/SampleDbContext.cs`
- Create: `src/EfUi.SampleHost/Data/SampleDbSeeder.cs`
- Create: `src/EfUi.SampleHost/appsettings.json`
- Modify: `src/EfUi.SampleHost/Program.cs`
- Test: `tests/EfUi.Core.Tests/TestDoubles/SampleModelDbContext.cs`

**Step 1: Write the failing test support type**

Create `tests/EfUi.Core.Tests/TestDoubles/SampleModelDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Tests.TestDoubles;

public sealed class SampleModelDbContext(DbContextOptions<SampleModelDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired();
            builder.Property(x => x.Email).IsRequired();
            builder.HasOne(x => x.Group).WithMany(x => x.Users).HasForeignKey(x => x.GroupId);
        });
    }
}

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? GroupId { get; set; }
    public Group? Group { get; set; }
}

public sealed class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<User> Users { get; set; } = new();
}
```

**Step 2: Mirror that model in the sample host**

Create the same entity shape under:
- `src/EfUi.SampleHost/Models/User.cs`
- `src/EfUi.SampleHost/Models/Group.cs`

Create `src/EfUi.SampleHost/Data/SampleDbContext.cs` with the same configuration pattern.

**Step 3: Add a simple SQLite seeder**

Create `src/EfUi.SampleHost/Data/SampleDbSeeder.cs`:
```csharp
public static class SampleDbSeeder
{
    public static async Task SeedAsync(SampleDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        var admins = new Group { Name = "Admins" };
        var guests = new Group { Name = "Guests" };

        db.Groups.AddRange(admins, guests);
        db.Users.AddRange(
            new User { Name = "Ada", Email = "ada@example.com", IsActive = true, CreatedAt = DateTime.UtcNow, Group = admins },
            new User { Name = "Linus", Email = "linus@example.com", IsActive = false, CreatedAt = DateTime.UtcNow, Group = guests });

        await db.SaveChangesAsync();
    }
}
```

**Step 4: Wire the sample host `Program.cs`**

Create the minimal host setup:
```csharp
using EfUi.SampleHost.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Sample") ?? "Data Source=sample.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await SampleDbSeeder.SeedAsync(db);
}

app.MapGet("/", () => Results.Redirect("/efui"));
app.Run();

public partial class Program;
```

**Step 5: Add configuration**

`src/EfUi.SampleHost/appsettings.json`
```json
{
  "ConnectionStrings": {
    "Sample": "Data Source=sample.db"
  }
}
```

**Step 6: Run the host once**

Run:
```bash
dotnet run --project src/EfUi.SampleHost
```

Expected: app starts and creates `sample.db`.

**Step 7: Commit**

```bash
git add src/EfUi.SampleHost tests/EfUi.Core.Tests/TestDoubles/SampleModelDbContext.cs
git commit -m "feat: add sample sqlite model"
```

## Task 3: Write failing metadata discovery tests

**Files:**
- Create: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Create: `src/EfUi.Core/Metadata/EntityMetadata.cs`
- Create: `src/EfUi.Core/Metadata/EntityPropertyMetadata.cs`
- Create: `src/EfUi.Core/Metadata/IEntityMetadataProvider.cs`
- Create: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`

**Step 1: Write the failing tests**

`tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
```csharp
using EfUi.Core.Metadata;
using EfUi.Core.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfUi.Core.Tests.Metadata;

public class EntityMetadataProviderTests
{
    [Fact]
    public void GetEntities_returns_user_and_group_entities()
    {
        using var db = CreateDb();
        var sut = new EfEntityMetadataProvider();

        var entities = sut.GetEntities(db).Select(x => x.RouteName).ToList();

        entities.Should().Contain(new[] { "users", "groups" });
    }

    [Fact]
    public void Editable_properties_exclude_key_and_navigation_properties()
    {
        using var db = CreateDb();
        var sut = new EfEntityMetadataProvider();

        var user = sut.GetEntity(db, "users");

        user.EditableProperties.Select(x => x.Name).Should().BeEquivalentTo("Name", "Email", "IsActive", "CreatedAt", "GroupId");
        user.EditableProperties.Select(x => x.Name).Should().NotContain(new[] { "Id", "Group" });
    }

    private static SampleModelDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SampleModelDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new SampleModelDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~EntityMetadataProviderTests
```

Expected: FAIL because `EfUi.Core.Metadata` types do not exist.

**Step 3: Write minimal implementation**

Create `src/EfUi.Core/Metadata/EntityPropertyMetadata.cs`:
```csharp
namespace EfUi.Core.Metadata;

public sealed record EntityPropertyMetadata(string Name, Type ClrType, bool IsEditable);
```

Create `src/EfUi.Core/Metadata/EntityMetadata.cs`:
```csharp
namespace EfUi.Core.Metadata;

public sealed record EntityMetadata(
    string DisplayName,
    string RouteName,
    Type ClrType,
    IReadOnlyList<EntityPropertyMetadata> AllProperties,
    IReadOnlyList<EntityPropertyMetadata> EditableProperties);
```

Create `src/EfUi.Core/Metadata/IEntityMetadataProvider.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Metadata;

public interface IEntityMetadataProvider
{
    IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext);
    EntityMetadata GetEntity(DbContext dbContext, string routeName);
}
```

Create `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Metadata;

public sealed class EfEntityMetadataProvider : IEntityMetadataProvider
{
    public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext)
    {
        return dbContext.Model.GetEntityTypes()
            .Where(x => x.ClrType.IsClass)
            .Select(Build)
            .OrderBy(x => x.RouteName)
            .ToList();
    }

    public EntityMetadata GetEntity(DbContext dbContext, string routeName)
    {
        return GetEntities(dbContext).Single(x => x.RouteName == routeName);
    }

    private static EntityMetadata Build(IMutableEntityType entityType)
    {
        var scalarProperties = entityType.GetProperties()
            .Select(p => new EntityPropertyMetadata(p.Name, p.ClrType, IsEditable(p)))
            .ToList();

        return new EntityMetadata(
            entityType.ClrType.Name,
            entityType.GetTableName()?.ToLowerInvariant() ?? entityType.ClrType.Name.ToLowerInvariant(),
            entityType.ClrType,
            scalarProperties,
            scalarProperties.Where(x => x.IsEditable).ToList());
    }

    private static bool IsEditable(IMutableProperty property)
        => !property.IsPrimaryKey()
           && !property.IsShadowProperty()
           && property.PropertyInfo?.SetMethod is not null;
}
```

**Step 4: Run test to verify it passes**

Run the same `dotnet test` command.

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.Core.Tests/Metadata src/EfUi.Core/Metadata
git commit -m "feat: add entity metadata discovery"
```

## Task 4: Tighten editability rules with a failing test

**Files:**
- Modify: `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- Modify: `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`

**Step 1: Add a failing test for unsupported edit types**

Add this test:
```csharp
[Fact]
public void Editable_properties_include_only_scalar_types()
{
    using var db = CreateDb();
    var sut = new EfEntityMetadataProvider();

    var group = sut.GetEntity(db, "groups");

    group.EditableProperties.Select(x => x.Name).Should().BeEquivalentTo("Name");
    group.EditableProperties.Select(x => x.Name).Should().NotContain("Users");
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~EntityMetadataProviderTests.Editable_properties_include_only_scalar_types
```

Expected: FAIL if collection or unsupported types are still treated as editable.

**Step 3: Write minimal implementation**

Change `IsEditable` to:
```csharp
private static bool IsEditable(IMutableProperty property)
    => !property.IsPrimaryKey()
       && !property.IsShadowProperty()
       && property.PropertyInfo?.SetMethod is not null
       && IsSupportedScalar(property.ClrType);

private static bool IsSupportedScalar(Type type)
{
    var actual = Nullable.GetUnderlyingType(type) ?? type;
    return actual.IsEnum
        || actual == typeof(string)
        || actual == typeof(bool)
        || actual == typeof(byte)
        || actual == typeof(short)
        || actual == typeof(int)
        || actual == typeof(long)
        || actual == typeof(float)
        || actual == typeof(double)
        || actual == typeof(decimal)
        || actual == typeof(DateTime)
        || actual == typeof(Guid);
}
```

**Step 4: Run the metadata tests**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~EntityMetadataProviderTests
```

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs
git commit -m "feat: restrict editable properties to supported scalars"
```

## Task 5: Add failing scalar binder tests

**Files:**
- Create: `tests/EfUi.Core.Tests/Binding/ScalarValueBinderTests.cs`
- Create: `src/EfUi.Core/Binding/BindResult.cs`
- Create: `src/EfUi.Core/Binding/IScalarValueBinder.cs`
- Create: `src/EfUi.Core/Binding/ScalarValueBinder.cs`

**Step 1: Write the failing tests**

`tests/EfUi.Core.Tests/Binding/ScalarValueBinderTests.cs`
```csharp
using EfUi.Core.Binding;
using FluentAssertions;
using Xunit;

namespace EfUi.Core.Tests.Binding;

public class ScalarValueBinderTests
{
    [Theory]
    [InlineData(typeof(string), "Ada", "Ada")]
    [InlineData(typeof(int), "42", 42)]
    [InlineData(typeof(bool), "true", true)]
    public void Bind_returns_typed_scalar_values(Type targetType, string raw, object expected)
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(targetType, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Bind_returns_failure_for_invalid_int()
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(typeof(int), "abc");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("int");
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~ScalarValueBinderTests
```

Expected: FAIL because binder types do not exist.

**Step 3: Write minimal implementation**

`src/EfUi.Core/Binding/BindResult.cs`
```csharp
namespace EfUi.Core.Binding;

public sealed record BindResult(bool IsSuccess, object? Value, string? Error)
{
    public static BindResult Success(object? value) => new(true, value, null);
    public static BindResult Failure(string error) => new(false, null, error);
}
```

`src/EfUi.Core/Binding/IScalarValueBinder.cs`
```csharp
namespace EfUi.Core.Binding;

public interface IScalarValueBinder
{
    BindResult Bind(Type targetType, string? rawValue);
}
```

`src/EfUi.Core/Binding/ScalarValueBinder.cs`
```csharp
using System.Globalization;

namespace EfUi.Core.Binding;

public sealed class ScalarValueBinder : IScalarValueBinder
{
    public BindResult Bind(Type targetType, string? rawValue)
    {
        var actual = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (actual == typeof(string)) return BindResult.Success(rawValue ?? string.Empty);
            if (string.IsNullOrWhiteSpace(rawValue) && Nullable.GetUnderlyingType(targetType) is not null)
                return BindResult.Success(null);
            if (actual == typeof(int)) return BindResult.Success(int.Parse(rawValue!, CultureInfo.InvariantCulture));
            if (actual == typeof(bool)) return BindResult.Success(bool.Parse(rawValue!));
            if (actual == typeof(DateTime)) return BindResult.Success(DateTime.Parse(rawValue!, CultureInfo.InvariantCulture));
            if (actual.IsEnum) return BindResult.Success(Enum.Parse(actual, rawValue!, ignoreCase: true));
        }
        catch
        {
            return BindResult.Failure($"Could not parse '{rawValue}' as {actual.Name}.");
        }

        return BindResult.Failure($"Type {actual.Name} is not supported.");
    }
}
```

**Step 4: Run the binder tests**

Run the same `dotnet test` command.

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.Core.Tests/Binding src/EfUi.Core/Binding
git commit -m "feat: add scalar value binder"
```

## Task 6: Add failing CRUD service tests

**Files:**
- Create: `tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`
- Create: `src/EfUi.Core/Crud/CrudOperationResult.cs`
- Create: `src/EfUi.Core/Crud/IEntityCrudService.cs`
- Create: `src/EfUi.Core/Crud/EntityCrudService.cs`

**Step 1: Write the failing tests**

`tests/EfUi.Core.Tests/Crud/EntityCrudServiceTests.cs`
```csharp
using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfUi.Core.Tests.Crud;

public class EntityCrudServiceTests
{
    [Fact]
    public async Task CreateAsync_persists_a_new_user()
    {
        await using var db = await CreateDbAsync();
        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var result = await sut.CreateAsync(db, "users", new Dictionary<string, string?>
        {
            ["Name"] = "Grace",
            ["Email"] = "grace@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["GroupId"] = "1"
        });

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_changes_only_writable_fields()
    {
        await using var db = await CreateDbAsync();
        db.Users.Add(new User { Name = "Ada", Email = "ada@old.test", IsActive = true, CreatedAt = new DateTime(2026, 5, 17) });
        await db.SaveChangesAsync();

        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());
        var id = db.Users.Single().Id;

        var result = await sut.UpdateAsync(db, "users", id, new Dictionary<string, string?>
        {
            ["Name"] = "Ada Lovelace",
            ["Email"] = "ada@example.com"
        });

        result.IsSuccess.Should().BeTrue();
        var user = await db.Users.SingleAsync();
        user.Name.Should().Be("Ada Lovelace");
        user.Email.Should().Be("ada@example.com");
    }

    [Fact]
    public async Task DeleteAsync_removes_the_row()
    {
        await using var db = await CreateDbAsync();
        db.Users.Add(new User { Name = "Ada", Email = "ada@example.com", IsActive = true, CreatedAt = new DateTime(2026, 5, 17) });
        await db.SaveChangesAsync();

        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());
        var id = db.Users.Single().Id;

        var result = await sut.DeleteAsync(db, "users", id);

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(0);
    }

    private static async Task<SampleModelDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<SampleModelDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new SampleModelDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Groups.Add(new Group { Name = "Admins" });
        await db.SaveChangesAsync();
        return db;
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~EntityCrudServiceTests
```

Expected: FAIL because CRUD service types do not exist.

**Step 3: Write minimal implementation**

`src/EfUi.Core/Crud/CrudOperationResult.cs`
```csharp
namespace EfUi.Core.Crud;

public sealed record CrudOperationResult(bool IsSuccess, IReadOnlyDictionary<string, string[]> Errors)
{
    public static CrudOperationResult Success() => new(true, new Dictionary<string, string[]>());
    public static CrudOperationResult Failure(string key, string error) => new(false, new Dictionary<string, string[]> { [key] = new[] { error } });
}
```

`src/EfUi.Core/Crud/IEntityCrudService.cs`
```csharp
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public interface IEntityCrudService
{
    Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values);
    Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values);
    Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key);
}
```

`src/EfUi.Core/Crud/EntityCrudService.cs`
```csharp
using EfUi.Core.Binding;
using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Crud;

public sealed class EntityCrudService(IEntityMetadataProvider metadataProvider, IScalarValueBinder binder) : IEntityCrudService
{
    public async Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values)
    {
        var entity = metadataProvider.GetEntity(dbContext, entityRoute);
        var instance = Activator.CreateInstance(entity.ClrType)!;
        var applyResult = ApplyValues(entity, instance, values);
        if (!applyResult.IsSuccess) return applyResult;

        dbContext.Add(instance);
        await dbContext.SaveChangesAsync();
        return CrudOperationResult.Success();
    }

    public async Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values)
    {
        var entity = metadataProvider.GetEntity(dbContext, entityRoute);
        var instance = await dbContext.FindAsync(entity.ClrType, key);
        if (instance is null) return CrudOperationResult.Failure("id", "Row not found.");

        var applyResult = ApplyValues(entity, instance, values);
        if (!applyResult.IsSuccess) return applyResult;

        await dbContext.SaveChangesAsync();
        return CrudOperationResult.Success();
    }

    public async Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key)
    {
        var entity = metadataProvider.GetEntity(dbContext, entityRoute);
        var instance = await dbContext.FindAsync(entity.ClrType, key);
        if (instance is null) return CrudOperationResult.Failure("id", "Row not found.");

        dbContext.Remove(instance);
        await dbContext.SaveChangesAsync();
        return CrudOperationResult.Success();
    }

    private CrudOperationResult ApplyValues(EntityMetadata entity, object instance, IReadOnlyDictionary<string, string?> values)
    {
        foreach (var property in entity.EditableProperties)
        {
            if (!values.TryGetValue(property.Name, out var raw)) continue;
            var bindResult = binder.Bind(property.ClrType, raw);
            if (!bindResult.IsSuccess)
                return CrudOperationResult.Failure(property.Name, bindResult.Error ?? "Invalid value.");

            instance.GetType().GetProperty(property.Name)!.SetValue(instance, bindResult.Value);
        }

        return CrudOperationResult.Success();
    }
}
```

**Step 4: Run CRUD tests**

Run the same `dotnet test` command.

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.Core.Tests/Crud src/EfUi.Core/Crud
git commit -m "feat: add scalar crud service"
```

## Task 7: Add failing HTML renderer tests

**Files:**
- Create: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Create: `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
- Create: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`

**Step 1: Write the failing tests**

`tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
```csharp
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using FluentAssertions;
using Xunit;

namespace EfUi.Core.Tests.Rendering;

public class HtmlPageRendererTests
{
    [Fact]
    public void RenderIndex_contains_entity_links()
    {
        var sut = new HtmlPageRenderer();
        var entities = new[]
        {
            new EntityMetadata("User", "users", typeof(object), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>()),
            new EntityMetadata("Group", "groups", typeof(object), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>())
        };

        var html = sut.RenderIndex("/efui", entities);

        html.Should().Contain("/efui/users");
        html.Should().Contain("/efui/groups");
    }

    [Fact]
    public void RenderForm_omits_read_only_fields()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "User",
            "users",
            typeof(object),
            new[]
            {
                new EntityPropertyMetadata("Id", typeof(int), false),
                new EntityPropertyMetadata("Name", typeof(string), true)
            },
            new[]
            {
                new EntityPropertyMetadata("Name", typeof(string), true)
            });

        var html = sut.RenderForm("/efui", metadata, null, isCreate: true, errors: new Dictionary<string, string[]>());

        html.Should().Contain("name=\"Name\"");
        html.Should().NotContain("name=\"Id\"");
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~HtmlPageRendererTests
```

Expected: FAIL because renderer types do not exist.

**Step 3: Write minimal implementation**

`src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
```csharp
using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public interface IHtmlPageRenderer
{
    string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities);
    string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors);
}
```

`src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
```csharp
using System.Net;
using System.Text;
using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public sealed class HtmlPageRenderer : IHtmlPageRenderer
{
    public string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body><h1>EF UI</h1><ul>");
        foreach (var entity in entities)
            sb.Append($"<li><a href=\"{routePrefix}/{entity.RouteName}\">{entity.DisplayName}</a></li>");
        sb.Append("</ul></body></html>");
        return sb.ToString();
    }

    public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body>");
        sb.Append($"<form method=\"post\" action=\"{routePrefix}/{entity.RouteName}\">");
        foreach (var property in entity.EditableProperties)
        {
            sb.Append($"<label>{WebUtility.HtmlEncode(property.Name)}</label>");
            sb.Append($"<input name=\"{property.Name}\" />");
        }
        sb.Append("<button type=\"submit\">Save</button></form></body></html>");
        return sb.ToString();
    }
}
```

**Step 4: Run renderer tests**

Run the same `dotnet test` command.

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.Core.Tests/Rendering src/EfUi.Core/Rendering
git commit -m "feat: add minimal html renderer"
```

## Task 8: Add failing ASP.NET integration tests

**Files:**
- Create: `tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs`
- Create: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Create: `src/EfUi.AspNetCore/EfUiOptions.cs`
- Create: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`

**Step 1: Write the failing tests**

`tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs`
```csharp
using Microsoft.AspNetCore.Mvc.Testing;

namespace EfUi.AspNetCore.Tests;

public sealed class EfUiApplicationFactory : WebApplicationFactory<Program>;
```

`tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
```csharp
using FluentAssertions;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public class EfUiEndpointsTests : IClassFixture<EfUiApplicationFactory>
{
    private readonly HttpClient _client;

    public EfUiEndpointsTests(EfUiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_index_returns_entity_links()
    {
        var html = await _client.GetStringAsync("/efui");
        html.Should().Contain("/efui/users");
        html.Should().Contain("/efui/groups");
    }

    [Fact]
    public async Task Post_create_user_redirects_back_to_entity_page()
    {
        var response = await _client.PostAsync("/efui/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Grace",
            ["Email"] = "grace@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["GroupId"] = "1"
        }));

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.SeeOther);
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: FAIL because `UseEfUi(...)` and endpoints do not exist.

**Step 3: Add minimal options and extension shell**

`src/EfUi.AspNetCore/EfUiOptions.cs`
```csharp
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.AspNetCore;

public sealed class EfUiOptions
{
    public Type DbContextType { get; set; } = null!;
    public string RoutePrefix { get; set; } = "/efui";
    public bool EnableInProduction { get; set; }
    public Func<IMutableEntityType, bool>? EntityFilter { get; set; }
    public Func<IProperty, bool>? PropertyFilter { get; set; }
}
```

`src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
```csharp
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EfUi.AspNetCore;

public static class EfUiApplicationBuilderExtensions
{
    public static WebApplication UseEfUi(this WebApplication app, Action<EfUiOptions> configure)
    {
        var options = new EfUiOptions();
        configure(options);

        if (!options.EnableInProduction && app.Environment.IsProduction())
            return app;

        app.MapGet(options.RoutePrefix, (IServiceProvider services) =>
        {
            var db = (DbContext)services.GetRequiredService(options.DbContextType);
            var metadata = new EfEntityMetadataProvider().GetEntities(db);
            var html = new HtmlPageRenderer().RenderIndex(options.RoutePrefix, metadata);
            return Results.Content(html, "text/html");
        });

        return app;
    }
}
```

**Step 4: Wire the sample host to the extension**

Modify `src/EfUi.SampleHost/Program.cs`:
```csharp
using EfUi.AspNetCore;
// existing usings...

app.UseEfUi(options =>
{
    options.DbContextType = typeof(SampleDbContext);
    options.RoutePrefix = "/efui";
    options.EnableInProduction = false;
});
```

**Step 5: Run the integration tests again**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Get_index_returns_entity_links
```

Expected: GET test passes, POST test still fails.

**Step 6: Commit**

```bash
git add src/EfUi.AspNetCore src/EfUi.SampleHost/Program.cs tests/EfUi.AspNetCore.Tests
git commit -m "feat: add ef ui index endpoint"
```

## Task 9: Implement list, create, edit, and delete HTTP endpoints

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Modify: `src/EfUi.Core/Rendering/IHtmlPageRenderer.cs`
- Modify: `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`

**Step 1: Add failing route tests one at a time**

Expand `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` with:
```csharp
[Fact]
public async Task Get_entity_page_renders_table()
{
    var html = await _client.GetStringAsync("/efui/users");
    html.Should().Contain("<table");
    html.Should().Contain("Ada");
}

[Fact]
public async Task Get_new_form_renders_only_editable_fields()
{
    var html = await _client.GetStringAsync("/efui/users/new");
    html.Should().Contain("name=\"Name\"");
    html.Should().NotContain("name=\"Id\"");
}

[Fact]
public async Task Post_delete_removes_user()
{
    var response = await _client.PostAsync("/efui/users/1/delete", new FormUrlEncodedContent(new Dictionary<string, string>()));
    response.IsSuccessStatusCode.Should().BeTrue();
}
```

**Step 2: Run only the new failing tests**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~EfUiEndpointsTests
```

Expected: FAIL on missing endpoints and missing table rendering.

**Step 3: Extend the renderer minimally**

Add methods to `IHtmlPageRenderer`:
```csharp
string RenderList(string routePrefix, EntityMetadata entity, IReadOnlyList<object> rows);
string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key);
```

Implement `RenderList` and `RenderEditForm` in `HtmlPageRenderer` using plain `<table>` and `<form>` markup. Use reflection for current cell values and only render `EditableProperties` in forms.

**Step 4: Implement the missing routes**

Update `UseEfUi(...)` to add:
- `GET {prefix}/{entity}`
- `GET {prefix}/{entity}/new`
- `GET {prefix}/{entity}/{id}/edit`
- `POST {prefix}/{entity}`
- `POST {prefix}/{entity}/{id}`
- `POST {prefix}/{entity}/{id}/delete`

Use this minimal form-value helper:
```csharp
static async Task<Dictionary<string, string?>> ReadFormAsync(HttpRequest request)
{
    var form = await request.ReadFormAsync();
    return form.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
}
```

Use `EntityCrudService` for writes and return either `Results.Redirect(...)` or `Results.Content(...)`.

**Step 5: Run the integration tests**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj
```

Expected: PASS.

**Step 6: Commit**

```bash
git add src/EfUi.AspNetCore src/EfUi.Core/Rendering tests/EfUi.AspNetCore.Tests
git commit -m "feat: add scalar crud ef ui endpoints"
```

## Task 10: Add production guard and one regression test

**Files:**
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`

**Step 1: Write one failing test for missing rows**

Add:
```csharp
[Fact]
public async Task Post_delete_missing_row_returns_not_found()
{
    var response = await _client.PostAsync("/efui/users/999/delete", new FormUrlEncodedContent(new Dictionary<string, string>()));
    response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
}
```

**Step 2: Run that test to verify it fails if behavior is wrong**

Run:
```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Post_delete_missing_row_returns_not_found
```

Expected: FAIL if delete returns success for missing rows.

**Step 3: Fix the HTTP mapping**

In `UseEfUi(...)`, map `CrudOperationResult.Failure("id", "Row not found.")` to:
```csharp
return Results.NotFound();
```

Also preserve the existing production guard:
```csharp
if (!options.EnableInProduction && app.Environment.IsProduction())
    return app;
```

**Step 4: Run all tests**

Run:
```bash
dotnet test EfUi.sln
```

Expected: PASS for all tests.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "fix: return not found for missing ef ui rows"
```

## Task 11: Manual verification and README stub

**Files:**
- Create: `README.md`
- Modify: `src/EfUi.SampleHost/Program.cs`

**Step 1: Write a tiny README stub**

`README.md`
```md
# EF UI

## Run

```bash
dotnet run --project src/EfUi.SampleHost
```

Open `http://localhost:5000/efui` or the HTTPS port shown by ASP.NET Core.

## Test

```bash
dotnet test EfUi.sln
```
```

**Step 2: Ensure the sample host root redirects to `/efui`**

If not already present, keep:
```csharp
app.MapGet("/", () => Results.Redirect("/efui"));
```

**Step 3: Run manual verification**

Run:
```bash
dotnet run --project src/EfUi.SampleHost
```

Verify manually:
- `/efui` lists `users` and `groups`
- `/efui/users` shows seeded rows
- creating a user works
- editing a user works
- deleting a user works
- forms do not expose `Id`

**Step 4: Run final automated verification**

Run:
```bash
dotnet test EfUi.sln
```

Expected: PASS.

**Step 5: Commit**

```bash
git add README.md src/EfUi.SampleHost/Program.cs
git commit -m "docs: add ef ui usage instructions"
```

## Optional Task 12: Add a Chinook validation host only if still simple

**Files:**
- Create: `src/EfUi.ChinookHost/EfUi.ChinookHost.csproj`
- Create: `src/EfUi.ChinookHost/Program.cs`
- Create: `src/EfUi.ChinookHost/Data/ChinookDbContext.cs`

**Step 1: Spike the mapping only after Task 11 passes**

Run:
```bash
dotnet new web -n EfUi.ChinookHost -o src/EfUi.ChinookHost
```

Expected: project exists.

**Step 2: Map a very small subset of Chinook**

Start with only one or two tables such as `artists` and `albums`. Do not attempt full schema coverage in the PoC.

Example:
```csharp
public sealed class Artist
{
    public int ArtistId { get; set; }
    public string? Name { get; set; }
}

public sealed class Album
{
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ArtistId { get; set; }
}
```

**Step 3: Point the host at the existing database**

Use:
```csharp
options.UseSqlite(@"Data Source=E:\Projets\ef-ui\db\chinook.db");
```

**Step 4: Manually verify `/efui`**

Run:
```bash
dotnet run --project src/EfUi.ChinookHost
```

Expected: `/efui` loads and lists the mapped entities.

**Step 5: Commit only if the spike stays small**

```bash
git add src/EfUi.ChinookHost
git commit -m "feat: add optional chinook validation host"
```

If the mapping starts to balloon, stop and leave Chinook out of the initial PoC.
