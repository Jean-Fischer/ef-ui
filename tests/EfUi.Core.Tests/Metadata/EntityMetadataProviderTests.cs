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

    [Fact]
    public void GetEntity_exposes_many_to_one_navigation_as_reference_field()
    {
        using var db = CreateDb();
        var sut = new EfEntityMetadataProvider();

        var user = sut.GetEntity(db, "users");

        user.UpdateEditableFields.Select(x => x.Name).Should().Contain("Group");
        user.UpdateEditableFields.Select(x => x.Name).Should().NotContain("GroupId");
        user.UpdateEditableFields.Single(x => x.Name == "Group").Kind.Should().Be(EditableFieldKind.Reference);
    }

    [Fact]
    public void GetEntity_uses_navigation_override_before_entity_default_for_fk_display_columns()
    {
        using var db = CreateDisplayColumnDb();
        var sut = new EfEntityMetadataProvider();

        var order = sut.GetEntity(db, "orders");

        var billingCustomerId = order.AllProperties.Single(property => property.Name == "BillingCustomerId");
        billingCustomerId.RelatedDisplayPropertyName.Should().Be("Code");
        billingCustomerId.RelatedRouteName.Should().Be("customers");

        var shippingCustomerId = order.AllProperties.Single(property => property.Name == "ShippingCustomerId");
        shippingCustomerId.RelatedDisplayPropertyName.Should().Be("Name");
        shippingCustomerId.RelatedRouteName.Should().Be("customers");

        order.UpdateEditableFields.Single(field => field.Name == "BillingCustomer").RelatedDisplayPropertyName.Should().Be("Code");
        order.UpdateEditableFields.Single(field => field.Name == "ShippingCustomer").RelatedDisplayPropertyName.Should().Be("Name");
    }

    [Fact]
    public void GetEntities_skips_shared_join_entities_without_single_primary_keys()
    {
        using var db = CreateManyToManyDb();
        var sut = new EfEntityMetadataProvider();

        var entities = sut.GetEntities(db).Select(x => x.RouteName).ToList();

        entities.Should().BeEquivalentTo(["playlists", "tracks"]);
    }

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

    [Fact]
    public void GetDiscoveryResult_reports_blocking_issues_for_regular_entities_with_composite_primary_keys()
    {
        using var db = CreateCompositeKeyDb();
        var sut = new EfEntityMetadataProvider();

        var discovery = sut.GetDiscoveryResult(db);

        discovery.Entities.Select(x => x.RouteName).Should().NotContain("memberships");
        discovery.BlockingIssues("memberships").Should().ContainSingle(issue =>
            issue.Message.Contains("composite primary key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetDiscoveryResult_reports_blocking_issues_for_keyless_entities()
    {
        using var db = CreateKeylessDb();
        var sut = new EfEntityMetadataProvider();

        var discovery = sut.GetDiscoveryResult(db);

        discovery.Entities.Select(x => x.RouteName).Should().NotContain("reports");
        discovery.BlockingIssues("reports").Should().ContainSingle(issue =>
            issue.Message.Contains("no primary key", StringComparison.OrdinalIgnoreCase));
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

    private static ManyToManyDbContext CreateManyToManyDb()
    {
        var options = new DbContextOptionsBuilder<ManyToManyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new ManyToManyDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static CompositeKeyDbContext CreateCompositeKeyDb()
    {
        var options = new DbContextOptionsBuilder<CompositeKeyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new CompositeKeyDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

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

    private static DisplayColumnDbContext CreateDisplayColumnDb()
    {
        var options = new DbContextOptionsBuilder<DisplayColumnDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new DisplayColumnDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static KeylessDbContext CreateKeylessDb()
    {
        var options = new DbContextOptionsBuilder<KeylessDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new KeylessDbContext(options);
        db.Database.OpenConnection();
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

    private sealed class ManyToManyDbContext(DbContextOptions<ManyToManyDbContext> options) : DbContext(options)
    {
        public DbSet<Playlist> Playlists => Set<Playlist>();

        public DbSet<Track> Tracks => Set<Track>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Playlist>()
                .HasMany(x => x.Tracks)
                .WithMany(x => x.Playlists)
                .UsingEntity<Dictionary<string, object>>(
                    "playlist_track",
                    right => right.HasOne<Track>().WithMany().HasForeignKey("TrackId"),
                    left => left.HasOne<Playlist>().WithMany().HasForeignKey("PlaylistId"),
                    join => join.HasKey("PlaylistId", "TrackId"));
        }
    }

    private sealed class CompositeKeyDbContext(DbContextOptions<CompositeKeyDbContext> options) : DbContext(options)
    {
        public DbSet<Membership> Memberships => Set<Membership>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Membership>(builder =>
            {
                builder.ToTable("memberships");
                builder.HasKey(x => new { x.UserId, x.GroupId });
            });
        }
    }

    private sealed class PayloadJoinDbContext(DbContextOptions<PayloadJoinDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().ToTable("orders");
            modelBuilder.Entity<Product>().ToTable("products");
            modelBuilder.Entity<OrderLine>().ToTable("order_lines");
        }
    }

    private sealed class KeylessDbContext(DbContextOptions<KeylessDbContext> options) : DbContext(options)
    {
        public DbSet<ReportRow> Reports => Set<ReportRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReportRow>().HasNoKey().ToTable("reports");
        }
    }

    private sealed class DisplayColumnDbContext(DbContextOptions<DisplayColumnDbContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<OrderWithDisplayColumns> Orders => Set<OrderWithDisplayColumns>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(builder =>
            {
                builder.ToTable("customers");
                builder.HasKey(x => x.Id);
            });

            modelBuilder.Entity<OrderWithDisplayColumns>(builder =>
            {
                builder.ToTable("orders");
                builder.HasKey(x => x.Id);
                builder.HasOne(x => x.BillingCustomer)
                    .WithMany()
                    .HasForeignKey(x => x.BillingCustomerId);
                builder.HasOne(x => x.ShippingCustomer)
                    .WithMany()
                    .HasForeignKey(x => x.ShippingCustomerId);
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

    private sealed class Playlist
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<Track> Tracks { get; set; } = [];
    }

    private sealed class Track
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<Playlist> Playlists { get; set; } = [];
    }

    private sealed class Membership
    {
        public int UserId { get; set; }
        public int GroupId { get; set; }
    }

    private sealed class ReportRow
    {
        public string Name { get; set; } = string.Empty;
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

    [EfUiDisplayColumn(nameof(Customer.Name))]
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OrderWithDisplayColumns
    {
        public int Id { get; set; }
        public int BillingCustomerId { get; set; }
        [EfUiDisplayColumn(nameof(Customer.Code))]
        public Customer BillingCustomer { get; set; } = null!;
        public int ShippingCustomerId { get; set; }
        public Customer ShippingCustomer { get; set; } = null!;
    }
}
