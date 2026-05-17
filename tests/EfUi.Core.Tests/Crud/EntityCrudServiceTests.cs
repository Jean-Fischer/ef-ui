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
