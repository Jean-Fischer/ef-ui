using EfUi.Core.Metadata;
using EfUi.Core.Query;
using EfUi.Core.Rendering;
using FluentAssertions;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public sealed class RenderedListViewAdapterTests
{
    [Fact]
    public void Create_maps_projected_cells_to_existing_rendered_list_view_contract()
    {
        var key = new EntityPropertyMetadata(nameof(TestUser.Id), typeof(int), false, false, IsPrimaryKey: true);
        var foreignKey = new EntityPropertyMetadata(
            nameof(TestUser.GroupId),
            typeof(int?),
            false,
            false,
            RelatedClrType: typeof(TestGroup),
            RelatedRouteName: "groups",
            RelatedDisplayPropertyName: "Name");
        var metadata = new EntityMetadata(
            "User",
            "users",
            typeof(TestUser),
            key,
            [key, foreignKey],
            []);
        var result = new EntityListQueryResult(
            [new EntityListQueryRow(
                "7",
                new Dictionary<string, EntityListQueryCell>
                {
                    [nameof(TestUser.Id)] = new("7", "7"),
                    [nameof(TestUser.GroupId)] = new("42", "The group", "groups")
                })],
            [new TableFilterClause("GroupId", "contains", "group")],
            [new TableSortClause("Id", "asc")],
            [new EntityListQueryError("query-warning", "A query warning.")],
            offset: 2,
            limit: 5);

        var view = RenderedListViewAdapter.Create(
            "/admin",
            metadata,
            result,
            ["A parser warning."]);

        view.Rows.Should().ContainSingle();
        view.Rows[0].Key.Should().Be("7");
        view.Rows[0].Cells[nameof(TestUser.GroupId)].Text.Should().Be("The group");
        view.Rows[0].Cells[nameof(TestUser.GroupId)].Href.Should().Be("/admin/groups/42/edit");
        view.Filters.Should().ContainSingle().Which.Field.Should().Be("GroupId");
        view.Sorts.Should().ContainSingle().Which.Field.Should().Be("Id");
        view.Errors.Should().Contain("A query warning.");
        view.Errors.Should().Contain("A parser warning.");
        view.Offset.Should().Be(2);
        view.Limit.Should().Be(5);
    }

    [Fact]
    public void Create_does_not_link_missing_related_rows()
    {
        var key = new EntityPropertyMetadata(nameof(TestUser.Id), typeof(int), false, false, IsPrimaryKey: true);
        var foreignKey = new EntityPropertyMetadata(
            nameof(TestUser.GroupId),
            typeof(int?),
            false,
            false,
            RelatedClrType: typeof(TestGroup),
            RelatedRouteName: "groups",
            RelatedDisplayPropertyName: "Name");
        var metadata = new EntityMetadata("User", "users", typeof(TestUser), key, [key, foreignKey], []);
        var result = new EntityListQueryResult(
            [new EntityListQueryRow(
                "7",
                new Dictionary<string, EntityListQueryCell>
                {
                    [nameof(TestUser.Id)] = new("7", "7"),
                    [nameof(TestUser.GroupId)] = new("999", "999")
                })]);

        var view = RenderedListViewAdapter.Create("/admin", metadata, result);

        view.Rows[0].Cells[nameof(TestUser.GroupId)].Href.Should().BeNull();
        view.Rows[0].Cells[nameof(TestUser.GroupId)].Text.Should().Be("999");
    }

    private sealed class TestUser
    {
        public int Id { get; set; }
        public int? GroupId { get; set; }
    }

    private sealed class TestGroup
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
