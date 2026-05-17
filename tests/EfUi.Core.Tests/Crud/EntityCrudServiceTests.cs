using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Data.Sqlite;
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
            ["Group"] = "1"
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
    public async Task UpdateAsync_updates_many_to_one_navigation_via_reference_field()
    {
        await using var db = await CreateDbAsync();
        db.Groups.Add(new Group { Name = "Guests" });
        db.Users.Add(new User
        {
            Name = "Ada",
            Email = "ada@example.com",
            IsActive = true,
            CreatedAt = new DateTime(2026, 5, 17),
            GroupId = 1
        });
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
    public async Task CreateAsync_returns_failure_for_duplicate_assigned_primary_key()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var seedDb = await CreateAssignedKeyDbAsync(connection))
        {
            seedDb.Tenants.Add(new AssignedKeyTenant { TenantKey = "tenant-north", Name = "North" });
            await seedDb.SaveChangesAsync();
        }

        await using var db = await CreateAssignedKeyDbAsync(connection);
        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var result = await sut.CreateAsync(db, "tenants", new Dictionary<string, string?>
        {
            ["TenantKey"] = "tenant-north",
            ["Name"] = "Duplicate North"
        });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainKey("persistence");
        result.Errors["persistence"].Should().ContainSingle().Which.Should().Be("Could not save changes.");
        (await db.Tenants.CountAsync()).Should().Be(1);
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

    [Fact]
    public async Task UpdateAsync_reconciles_supported_many_to_many_collection_from_repeated_values()
    {
        await using var db = await CreateManyToManyDbAsync();

        var track1 = new PlaylistTrackItem { Name = "Track A" };
        var track2 = new PlaylistTrackItem { Name = "Track B" };
        var track3 = new PlaylistTrackItem { Name = "Track C" };
        var playlist = new PlaylistWithTracks { Name = "Favorites", Tracks = [track1] };

        db.Tracks.AddRange(track1, track2, track3);
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();

        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var result = await sut.UpdateAsync(db, "playlists", playlist.Id, new Dictionary<string, string[]>
        {
            ["Tracks"] = ["2", "3"]
        });

        result.IsSuccess.Should().BeTrue();

        var updated = await db.Playlists.Include(x => x.Tracks).SingleAsync();
        updated.Tracks.Select(x => x.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public async Task UpdateAsync_clears_supported_many_to_many_collection_when_no_values_are_posted()
    {
        await using var db = await CreateManyToManyDbAsync();

        var track1 = new PlaylistTrackItem { Name = "Track A" };
        var track2 = new PlaylistTrackItem { Name = "Track B" };
        var playlist = new PlaylistWithTracks { Name = "Favorites", Tracks = [track1, track2] };

        db.Tracks.AddRange(track1, track2);
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();

        var sut = new EntityCrudService(new EfEntityMetadataProvider(), new ScalarValueBinder());

        var result = await sut.UpdateAsync(db, "playlists", playlist.Id, new Dictionary<string, string[]>
        {
            ["Tracks"] = []
        });

        result.IsSuccess.Should().BeTrue();

        var updated = await db.Playlists.Include(x => x.Tracks).SingleAsync();
        updated.Tracks.Should().BeEmpty();
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
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return await CreateAssignedKeyDbAsync(connection, ownsConnection: true);
    }

    private static async Task<ManyToManyCrudDbContext> CreateManyToManyDbAsync()
    {
        var options = new DbContextOptionsBuilder<ManyToManyCrudDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new ManyToManyCrudDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<AssignedKeyDbContext> CreateAssignedKeyDbAsync(SqliteConnection connection, bool ownsConnection = false)
    {
        var options = new DbContextOptionsBuilder<AssignedKeyDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AssignedKeyDbContext(options, ownsConnection ? connection : null);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static Task<AssignedKeyDbContext> CreateAssignedKeyDbAsync(SqliteConnection connection)
        => CreateAssignedKeyDbAsync(connection, ownsConnection: false);

    private sealed class AssignedKeyDbContext(DbContextOptions<AssignedKeyDbContext> options, SqliteConnection? ownedConnection = null) : DbContext(options)
    {
        private readonly SqliteConnection? _ownedConnection = ownedConnection;

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

        public override void Dispose()
        {
            base.Dispose();
            _ownedConnection?.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (_ownedConnection is not null)
            {
                await _ownedConnection.DisposeAsync();
            }
        }
    }

    private sealed class AssignedKeyTenant
    {
        public string TenantKey { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ManyToManyCrudDbContext(DbContextOptions<ManyToManyCrudDbContext> options) : DbContext(options)
    {
        public DbSet<PlaylistWithTracks> Playlists => Set<PlaylistWithTracks>();
        public DbSet<PlaylistTrackItem> Tracks => Set<PlaylistTrackItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlaylistWithTracks>()
                .ToTable("playlists")
                .HasMany(x => x.Tracks)
                .WithMany(x => x.Playlists)
                .UsingEntity<Dictionary<string, object>>(
                    "playlist_track",
                    right => right.HasOne<PlaylistTrackItem>().WithMany().HasForeignKey("TrackId"),
                    left => left.HasOne<PlaylistWithTracks>().WithMany().HasForeignKey("PlaylistId"),
                    join => join.HasKey("PlaylistId", "TrackId"));

            modelBuilder.Entity<PlaylistTrackItem>().ToTable("tracks");
        }
    }

    private sealed class PlaylistWithTracks
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<PlaylistTrackItem> Tracks { get; set; } = [];
    }

    private sealed class PlaylistTrackItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<PlaylistWithTracks> Playlists { get; set; } = [];
    }
}
