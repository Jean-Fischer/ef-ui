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
    public void GetEntities_uses_table_names_instead_of_dbset_property_names_for_routes()
    {
        using var db = CreateRouteNameDb();
        var sut = new EfEntityMetadataProvider();

        var entities = sut.GetEntities(db).Select(x => x.RouteName).ToList();

        entities.Should().Contain("app_users");
        entities.Should().NotContain("accounts");
    }

    [Fact]
    public void GetEntity_exposes_primary_key_metadata_for_non_conventional_key_names()
    {
        using var db = CreateAlternateKeyDb();
        var sut = new EfEntityMetadataProvider();

        var tenant = sut.GetEntity(db, "tenants");

        tenant.PrimaryKeyProperty.Name.Should().Be("TenantKey");
        tenant.PrimaryKeyProperty.IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Assigned_primary_key_is_createable_but_not_updateable()
    {
        using var db = CreateAlternateKeyDb();
        var sut = new EfEntityMetadataProvider();

        var tenant = sut.GetEntity(db, "tenants");

        tenant.CreateEditableProperties.Select(x => x.Name).Should().Contain("TenantKey");
        tenant.UpdateEditableProperties.Select(x => x.Name).Should().NotContain("TenantKey");
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

    [Fact]
    public void Editable_properties_exclude_unsupported_scalar_types_present_in_ef_metadata()
    {
        using var db = CreateUnsupportedScalarDb();
        var sut = new EfEntityMetadataProvider();

        var asset = sut.GetEntity(db, "assets");

        asset.AllProperties.Select(x => x.Name).Should().Contain("Payload");
        asset.EditableProperties.Select(x => x.Name).Should().Contain("Name");
        asset.EditableProperties.Select(x => x.Name).Should().NotContain("Payload");
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

    private static RouteNameDbContext CreateRouteNameDb()
    {
        var options = new DbContextOptionsBuilder<RouteNameDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new RouteNameDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static UnsupportedScalarDbContext CreateUnsupportedScalarDb()
    {
        var options = new DbContextOptionsBuilder<UnsupportedScalarDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new UnsupportedScalarDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static AlternateKeyDbContext CreateAlternateKeyDb()
    {
        var options = new DbContextOptionsBuilder<AlternateKeyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new AlternateKeyDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class RouteNameDbContext(DbContextOptions<RouteNameDbContext> options) : DbContext(options)
    {
        public DbSet<User> Accounts => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToTable("app_users");
        }
    }

    private sealed class UnsupportedScalarDbContext(DbContextOptions<UnsupportedScalarDbContext> options) : DbContext(options)
    {
        public DbSet<Asset> Assets => Set<Asset>();
    }

    private sealed class AlternateKeyDbContext(DbContextOptions<AlternateKeyDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>(builder =>
            {
                builder.HasKey(x => x.TenantKey);
                builder.Property(x => x.TenantKey).ValueGeneratedNever();
            });
        }
    }

    private sealed class Asset
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    private sealed class Tenant
    {
        public string TenantKey { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
