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
