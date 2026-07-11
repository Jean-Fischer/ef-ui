using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Tests.TestDoubles;

public sealed class SampleModelDbContext(DbContextOptions<SampleModelDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<ProviderRecord> ProviderRecords => Set<ProviderRecord>();

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

public sealed class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<User> Users { get; set; } = new();
}
