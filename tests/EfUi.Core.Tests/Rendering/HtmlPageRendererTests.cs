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
            new EntityMetadata("User", "users", typeof(object), new EntityPropertyMetadata("Id", typeof(int), false, true), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>()),
            new EntityMetadata("Group", "groups", typeof(object), new EntityPropertyMetadata("Id", typeof(int), false, true), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>())
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
            new EntityPropertyMetadata("Id", typeof(int), false, true),
            new[]
            {
                new EntityPropertyMetadata("Id", typeof(int), false, true),
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

    [Fact]
    public void RenderList_uri_escapes_primary_key_values_in_action_links()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "Tenant",
            "tenants",
            typeof(TenantRow),
            new EntityPropertyMetadata("TenantKey", typeof(string), false, true),
            new[]
            {
                new EntityPropertyMetadata("TenantKey", typeof(string), false, true),
                new EntityPropertyMetadata("GroupId", typeof(int), true),
                new EntityPropertyMetadata("Name", typeof(string), true)
            },
            new[]
            {
                new EntityPropertyMetadata("GroupId", typeof(int), true),
                new EntityPropertyMetadata("Name", typeof(string), true)
            });

        var html = sut.RenderList("/efui", metadata, new object[]
        {
            new TenantRow { TenantKey = "tenant / north?1", GroupId = 7, Name = "North" }
        });

        html.Should().Contain("/efui/tenants/tenant%20%2F%20north%3F1/edit");
        html.Should().Contain("/efui/tenants/tenant%20%2F%20north%3F1/delete");
        html.Should().NotContain("/efui/tenants/tenant / north?1/edit");
        html.Should().NotContain("/efui/tenants/7/edit");
    }

    [Fact]
    public void RenderEditForm_uri_escapes_primary_key_values_in_action_url()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "Tenant",
            "tenants",
            typeof(TenantRow),
            new EntityPropertyMetadata("TenantKey", typeof(string), false, true),
            new[]
            {
                new EntityPropertyMetadata("TenantKey", typeof(string), false, true),
                new EntityPropertyMetadata("Name", typeof(string), true)
            },
            new[]
            {
                new EntityPropertyMetadata("Name", typeof(string), true)
            });

        var html = sut.RenderEditForm("/efui", metadata, new TenantRow { TenantKey = "tenant / north?1", Name = "North" }, isCreate: false, errors: new Dictionary<string, string[]>(), key: "tenant / north?1");

        html.Should().Contain("action=\"/efui/tenants/tenant%20%2F%20north%3F1\"");
        html.Should().NotContain("action=\"/efui/tenants/tenant / north?1\"");
    }

    private sealed class TenantRow
    {
        public string TenantKey { get; init; } = string.Empty;
        public int GroupId { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
