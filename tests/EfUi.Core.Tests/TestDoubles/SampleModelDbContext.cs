using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Tests.TestDoubles;

public sealed class SampleModelDbContext(DbContextOptions<SampleModelDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<ProviderRecord> ProviderRecords => Set<ProviderRecord>();
    public DbSet<ScalarUser> ScalarUsers => Set<ScalarUser>();
    public DbSet<ScalarGroup> ScalarGroups => Set<ScalarGroup>();
    public DbSet<MultiReferenceUser> MultiReferenceUsers => Set<MultiReferenceUser>();
    public DbSet<ShadowKeyUser> ShadowKeyUsers => Set<ShadowKeyUser>();
    public DbSet<ShadowKeyGroup> ShadowKeyGroups => Set<ShadowKeyGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProviderRecord>(builder =>
        {
            builder.ToTable("provider_records");
            builder.HasKey(x => x.Id);
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired();
            builder.Property(x => x.Email).IsRequired();
            builder.HasOne(x => x.Group)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.GroupId);
        });

        modelBuilder.Entity<ScalarUser>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.HasOne<ScalarGroup>()
                .WithMany()
                .HasForeignKey(x => x.GroupId);
        });

        modelBuilder.Entity<MultiReferenceUser>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.PrimaryGroup)
                .WithMany()
                .HasForeignKey(x => x.PrimaryGroupId);
            builder.HasOne(x => x.SecondaryGroup)
                .WithMany()
                .HasForeignKey(x => x.SecondaryGroupId);
        });

        modelBuilder.Entity<ShadowKeyGroup>(builder =>
        {
            builder.Property<int>("ShadowId");
            builder.HasKey("ShadowId");
        });

        modelBuilder.Entity<ShadowKeyUser>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.HasOne<ShadowKeyGroup>()
                .WithMany()
                .HasForeignKey(x => x.ShadowGroupId)
                .HasPrincipalKey("ShadowId");
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

public enum ProviderRole
{
    Viewer,
    Editor
}

public sealed class ProviderRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? NullableNumber { get; set; }
    public bool IsActive { get; set; }
    public ProviderRole Role { get; set; }
    public Guid ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
}

[EfUiDisplayColumn(nameof(Name))]
public sealed class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ComputedLabel => $"{Name} (computed)";
    public List<User> Users { get; set; } = new();
}

[EfUiDisplayColumn(nameof(Name))]
public sealed class ScalarGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ScalarUser
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
}

public sealed class MultiReferenceUser
{
    public int Id { get; set; }
    public int? PrimaryGroupId { get; set; }
    public int? SecondaryGroupId { get; set; }
    public Group? PrimaryGroup { get; set; }

    [EfUiDisplayColumn(nameof(Group.Code))]
    public Group? SecondaryGroup { get; set; }
}

[EfUiDisplayColumn(nameof(Name))]
public sealed class ShadowKeyGroup
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ShadowKeyUser
{
    public int Id { get; set; }
    public int ShadowGroupId { get; set; }
}
