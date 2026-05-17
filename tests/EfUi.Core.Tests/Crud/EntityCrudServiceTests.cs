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
        var createdAt = new DateTime(2026, 5, 17, 10, 0, 0);

        var result = await sut.CreateAsync(db, "users", new Dictionary<string, string?>
        {
            ["Name"] = "Grace",
            ["Email"] = "grace@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["GroupId"] = "1"
        });

        result.IsSuccess.Should().BeTrue();

        var user = await db.Users.SingleAsync();
        user.Name.Should().Be("Grace");
        user.Email.Should().Be("grace@example.com");
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().Be(createdAt);
        user.GroupId.Should().Be(1);
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

    [Fact]
    public async Task UpdateAsync_with_invalid_value_does_not_mutate_existing_entity()
    {
        await using var db = await CreateDbAsync();
        var originalCreatedAt = new DateTime(2026, 5, 17, 10, 0, 0);
        db.Users.Add(new User
        {
            Name = "Original Name",
            Email = "original@example.com",
            IsActive = true,
            CreatedAt = originalCreatedAt,
            GroupId = 1
        });
        await db.SaveChangesAsync();

        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());
        var id = db.Users.Single().Id;

        var result = await sut.UpdateAsync(db, "users", id, new Dictionary<string, string?>
        {
            ["Name"] = "Edited Name",
            ["Email"] = "edited@example.com",
            ["CreatedAt"] = "not-a-date"
        });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainKey("CreatedAt");

        var user = await db.Users.SingleAsync();
        user.Name.Should().Be("Original Name");
        user.Email.Should().Be("original@example.com");
        user.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public async Task CreateAsync_returns_failure_for_unknown_entity()
    {
        await using var db = await CreateDbAsync();
        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var act = () => sut.CreateAsync(db, "missing-entities", new Dictionary<string, string?>());

        var result = await act.Should().NotThrowAsync();
        result.Which.IsSuccess.Should().BeFalse();
        result.Which.Errors.Should().ContainKey("entity");
        result.Which.Errors["entity"].Should().ContainSingle().Which.Should().Contain("missing-entities");
    }

    [Fact]
    public async Task CreateAsync_persists_assigned_primary_key_values_for_non_generated_keys()
    {
        await using var db = await CreateAssignedKeyDbAsync();
        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var result = await sut.CreateAsync(db, "tenants", new Dictionary<string, string?>
        {
            ["TenantKey"] = "tenant-south",
            ["Name"] = "South"
        });

        result.IsSuccess.Should().BeTrue();
        var tenant = await db.Tenants.SingleAsync();
        tenant.TenantKey.Should().Be("tenant-south");
        tenant.Name.Should().Be("South");
    }

    [Fact]
    public async Task UpdateAsync_does_not_allow_primary_key_edits_for_assigned_key_entities()
    {
        await using var db = await CreateAssignedKeyDbAsync();
        db.Tenants.Add(new AssignedKeyTenant { TenantKey = "tenant-north", Name = "North" });
        await db.SaveChangesAsync();

        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var result = await sut.UpdateAsync(db, "tenants", "tenant-north", new Dictionary<string, string?>
        {
            ["TenantKey"] = "tenant-south",
            ["Name"] = "North Updated"
        });

        result.IsSuccess.Should().BeTrue();
        var tenant = await db.Tenants.SingleAsync();
        tenant.TenantKey.Should().Be("tenant-north");
        tenant.Name.Should().Be("North Updated");
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

    private static async Task<AssignedKeyDbContext> CreateAssignedKeyDbAsync()
    {
        var options = new DbContextOptionsBuilder<AssignedKeyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new AssignedKeyDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class AssignedKeyDbContext(DbContextOptions<AssignedKeyDbContext> options) : DbContext(options)
    {
        public DbSet<AssignedKeyTenant> Tenants => Set<AssignedKeyTenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AssignedKeyTenant>(builder =>
            {
                builder.ToTable("tenants");
                builder.HasKey(x => x.TenantKey);
                builder.Property(x => x.TenantKey).ValueGeneratedNever();
                builder.Property(x => x.Name).IsRequired();
            });
        }
    }

    private sealed class AssignedKeyTenant
    {
        public string TenantKey { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
