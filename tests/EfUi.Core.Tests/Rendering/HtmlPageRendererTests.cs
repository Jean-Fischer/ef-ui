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
            new EntityMetadata("User", "users", typeof(object), PrimaryKey("Id", typeof(int)), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>()),
            new EntityMetadata("Group", "groups", typeof(object), PrimaryKey("Id", typeof(int)), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>())
        };

        var html = sut.RenderIndex("/efui", entities);

        html.Should().Contain("/efui/users");
        html.Should().Contain("/efui/groups");
    }

    [Fact]
    public void RenderForm_omits_store_generated_primary_key_fields_on_create()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "User",
            "users",
            typeof(object),
            PrimaryKey("Id", typeof(int)),
            new[]
            {
                PrimaryKey("Id", typeof(int)),
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            });

        var html = sut.RenderForm("/efui", metadata, null, isCreate: true, errors: new Dictionary<string, string[]>());

        html.Should().Contain("name=\"Name\"");
        html.Should().NotContain("name=\"Id\"");
    }

    [Fact]
    public void RenderForm_includes_assigned_primary_key_on_create_but_not_on_update()
    {
        var sut = new HtmlPageRenderer();
        var tenantKey = AssignedKey("TenantKey", typeof(string));
        var metadata = new EntityMetadata(
            "Tenant",
            "tenants",
            typeof(TenantRow),
            tenantKey,
            new[]
            {
                tenantKey,
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            });

        var createHtml = sut.RenderForm("/efui", metadata, null, isCreate: true, errors: new Dictionary<string, string[]>());
        var updateHtml = sut.RenderEditForm("/efui", metadata, new TenantRow { TenantKey = "tenant-1", Name = "North" }, isCreate: false, errors: new Dictionary<string, string[]>(), key: "tenant-1");

        createHtml.Should().Contain("name=\"TenantKey\"");
        updateHtml.Should().NotContain("name=\"TenantKey\"");
        updateHtml.Should().Contain("name=\"Name\"");
    }

    [Fact]
    public void RenderEditForm_prefers_submitted_values_over_model_values()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "User",
            "users",
            typeof(UserRow),
            PrimaryKey("Id", typeof(int)),
            new[]
            {
                PrimaryKey("Id", typeof(int)),
                Editable("Name", typeof(string)),
                Editable("Email", typeof(string)),
                Editable("CreatedAt", typeof(DateTime))
            },
            new[]
            {
                Editable("Name", typeof(string)),
                Editable("Email", typeof(string)),
                Editable("CreatedAt", typeof(DateTime))
            });

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new UserRow { Id = 7, Name = "Original", Email = "original@example.com", CreatedAt = new DateTime(2026, 5, 17, 10, 0, 0) },
            isCreate: false,
            errors: new Dictionary<string, string[]> { ["CreatedAt"] = new[] { "Invalid value." } },
            key: 7,
            submittedValues: new Dictionary<string, string?>
            {
                ["Name"] = "Edited",
                ["Email"] = "edited@example.com",
                ["CreatedAt"] = "not-a-date"
            });

        html.Should().Contain("name=\"Name\" value=\"Edited\"");
        html.Should().Contain("name=\"Email\" value=\"edited@example.com\"");
        html.Should().Contain("name=\"CreatedAt\" value=\"not-a-date\"");
        html.Should().NotContain("original@example.com");
    }

    [Fact]
    public void RenderList_uri_escapes_primary_key_values_in_action_links()
    {
        var sut = new HtmlPageRenderer();
        var tenantKey = AssignedKey("TenantKey", typeof(string));
        var metadata = new EntityMetadata(
            "Tenant",
            "tenants",
            typeof(TenantRow),
            tenantKey,
            new[]
            {
                tenantKey,
                Editable("GroupId", typeof(int)),
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("GroupId", typeof(int)),
                Editable("Name", typeof(string))
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
        var tenantKey = AssignedKey("TenantKey", typeof(string));
        var metadata = new EntityMetadata(
            "Tenant",
            "tenants",
            typeof(TenantRow),
            tenantKey,
            new[]
            {
                tenantKey,
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            });

        var html = sut.RenderEditForm("/efui", metadata, new TenantRow { TenantKey = "tenant / north?1", Name = "North" }, isCreate: false, errors: new Dictionary<string, string[]>(), key: "tenant / north?1");

        html.Should().Contain("action=\"/efui/tenants/tenant%20%2F%20north%3F1\"");
        html.Should().NotContain("action=\"/efui/tenants/tenant / north?1\"");
    }

    private static EntityPropertyMetadata PrimaryKey(string name, Type clrType)
        => new(name, clrType, IsEditableOnCreate: false, IsEditableOnUpdate: false, IsPrimaryKey: true);

    private static EntityPropertyMetadata AssignedKey(string name, Type clrType)
        => new(name, clrType, IsEditableOnCreate: true, IsEditableOnUpdate: false, IsPrimaryKey: true);

    private static EntityPropertyMetadata Editable(string name, Type clrType)
        => new(name, clrType, IsEditableOnCreate: true, IsEditableOnUpdate: true);

    private sealed class UserRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    private sealed class TenantRow
    {
        public string TenantKey { get; init; } = string.Empty;
        public int GroupId { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
