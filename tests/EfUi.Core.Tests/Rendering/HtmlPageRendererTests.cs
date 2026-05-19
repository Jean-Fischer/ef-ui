using System.Text.Json;
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
    public void RenderIndex_includes_theme_stylesheet_semantic_shell_and_breadcrumbs()
    {
        var sut = new HtmlPageRenderer();
        var entities = new[]
        {
            new EntityMetadata("User", "users", typeof(object), PrimaryKey("Id", typeof(int)), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>())
        };

        var html = sut.RenderIndex("/efui", entities);

        html.Should().Contain("href=\"/efui/assets/efui.css\"");
        html.Should().Contain("class=\"efui-body\"");
        html.Should().Contain("<main class=\"efui-page\">");
        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<section class=\"efui-surface\">");
        html.Should().Contain("<ul class=\"efui-index-list efui-link-grid\">");
    }

    [Fact]
    public void RenderIndex_formats_nested_route_prefixes_into_readable_breadcrumb_labels()
    {
        var sut = new HtmlPageRenderer();
        var entities = new[]
        {
            new EntityMetadata("User", "users", typeof(object), PrimaryKey("Id", typeof(int)), Array.Empty<EntityPropertyMetadata>(), Array.Empty<EntityPropertyMetadata>())
        };

        var html = sut.RenderIndex("/admin/ef-ui", entities);

        html.Should().Contain("<span class=\"efui-breadcrumb-current\">Admin Ef Ui</span>");
    }

    [Fact]
    public void RenderList_includes_breadcrumbs_table_status_and_semantic_table_classes()
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
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            });

        var html = sut.RenderList("/efui", metadata, new RenderedListView(
            new[]
            {
                new RenderedListRow("7", new Dictionary<string, RenderedListCell>
                {
                    ["Id"] = Cell("7"),
                    ["Name"] = Cell("Ada", "/efui/users/7/edit")
                })
            },
            new[]
            {
                new RenderedListFilter("Name", "contains", "Ada")
            },
            new[]
            {
                new RenderedListSort("Name", "asc")
            }));

        html.Should().Contain("href=\"/efui/assets/efui.css\"");
        html.Should().Contain("class=\"efui-body\"");
        html.Should().Contain("<main class=\"efui-page\">");
        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/efui\">Efui</a>");
        html.Should().Contain("<span class=\"efui-breadcrumb-current\">User</span>");
        html.Should().Contain("<section class=\"efui-surface\">");
        html.Should().Contain("<div class=\"efui-page-actions\">");
        html.Should().Contain("<a class=\"efui-primary-link\" href=\"/efui/users/new\">Create New</a>");
        html.Should().Contain("<section class=\"efui-table-status\"");
        html.Should().Contain("Name contains Ada");
        html.Should().Contain("Name asc");
        html.Should().NotContain("class=\"efui-query-builder\"");
        html.Should().NotContain("efui-query-builder-form");
        html.Should().NotContain("data-role=\"efui-query-form\"");
        html.Should().Contain("data-role=\"efui-table-loading\"");
        html.Should().Contain("<div class=\"efui-table-wrapper\" data-role=\"efui-table-fallback\">");
        html.Should().Contain("<table class=\"efui-table\">");
        html.Should().Contain("<td class=\"efui-row-actions\">");
        html.Should().Contain("<a class=\"efui-row-action-link\" href=\"/efui/users/7/edit\">Edit</a>");
        html.Should().Contain("<button class=\"efui-row-action-button\" type=\"submit\">Delete</button>");
        html.Should().Contain("\"field\":\"__actions\"");
    }

    [Fact]
    public void RenderList_emits_explicit_tabulator_column_metadata_for_active_filters_and_actions()
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
                Editable("Email", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string)),
                Editable("Email", typeof(string))
            });

        var html = sut.RenderList("/efui", metadata, new RenderedListView(
            new[]
            {
                new RenderedListRow("7", new Dictionary<string, RenderedListCell>
                {
                    ["Id"] = Cell("7"),
                    ["Name"] = Cell("Ada"),
                    ["Email"] = Cell("ada@example.com")
                })
            },
            new[]
            {
                new RenderedListFilter("Name", "contains", "Ada")
            },
            new[]
            {
                new RenderedListSort("Email", "desc")
            },
            Offset: 0,
            Limit: 25));

        using var config = GetTableConfig(html);
        var root = config.RootElement;
        root.GetProperty("listUrl").GetString().Should().Be("/efui/users");

        var nameColumn = GetColumn(root, "Name");
        nameColumn.GetProperty("title").GetString().Should().Be("Name");
        nameColumn.GetProperty("headerSort").GetBoolean().Should().BeTrue();
        nameColumn.GetProperty("headerFilter").GetString().Should().Be("input");
        nameColumn.GetProperty("filterOperator").GetString().Should().Be("contains");
        nameColumn.GetProperty("headerFilterValue").GetString().Should().Be("Ada");

        var actionsColumn = GetColumn(root, "__actions");
        actionsColumn.GetProperty("headerSort").GetBoolean().Should().BeFalse();
        actionsColumn.GetProperty("headerFilter").ValueKind.Should().Be(JsonValueKind.False);
        actionsColumn.GetProperty("filterOperator").ValueKind.Should().Be(JsonValueKind.Null);

        var activeSort = root.GetProperty("query").GetProperty("sorts").EnumerateArray().Single();
        activeSort.GetProperty("Field").GetString().Should().Be("Email");
        activeSort.GetProperty("Direction").GetString().Should().Be("desc");
    }

    [Fact]
    public void RenderList_renders_linked_cells_from_prepared_display_values()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "Album",
            "albums",
            typeof(object),
            PrimaryKey("Id", typeof(int)),
            new[]
            {
                PrimaryKey("Id", typeof(int)),
                Editable("ArtistId", typeof(int)),
                Editable("Title", typeof(string))
            },
            new[]
            {
                Editable("ArtistId", typeof(int)),
                Editable("Title", typeof(string))
            });

        var html = sut.RenderList("/efui", metadata, new RenderedListView(
            new[]
            {
                new RenderedListRow("1", new Dictionary<string, RenderedListCell>
                {
                    ["Id"] = Cell("1"),
                    ["ArtistId"] = Cell("AC/DC", "/efui/artists/1/edit"),
                    ["Title"] = Cell("For Those About To Rock")
                })
            }));

        html.Should().Contain("<a class=\"efui-cell-link\" href=\"/efui/artists/1/edit\">AC/DC</a>");
        html.Should().Contain(">For Those About To Rock<");
        html.Should().NotContain(">17<");
    }

    [Fact]
    public void HtmlPageRenderer_exposes_only_rendered_list_view_RenderList_overload()
    {
        var overloads = typeof(HtmlPageRenderer)
            .GetMethods()
            .Where(method => method.Name == nameof(HtmlPageRenderer.RenderList))
            .ToArray();

        overloads.Should().ContainSingle();
        overloads[0].GetParameters()[2].ParameterType.Should().Be(typeof(RenderedListView));
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
    public void RenderForm_includes_assigned_primary_key_on_create_and_shows_it_read_only_on_update()
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
        updateHtml.Should().Contain("TenantKey");
        updateHtml.Should().Contain(">tenant-1<");
        updateHtml.Should().NotContain("name=\"TenantKey\"");
        updateHtml.Should().Contain("name=\"Name\"");
    }

    [Fact]
    public void RenderEditForm_shows_generated_primary_key_as_read_only()
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
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            });

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new UserRow { Id = 7, Name = "Ada" },
            isCreate: false,
            errors: new Dictionary<string, string[]>(),
            key: 7);

        html.Should().Contain("Id");
        html.Should().Contain(">7<");
        html.Should().NotContain("name=\"Id\"");
    }

    [Fact]
    public void RenderEditForm_renders_reference_fields_as_dropdowns()
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
                Editable("GroupId", typeof(int?))
            },
            new[]
            {
                Editable("Name", typeof(string)),
                Editable("GroupId", typeof(int?))
            },
            new[]
            {
                ScalarField("Name", typeof(string)),
                ReferenceField("Group", typeof(int?), typeof(TenantRow), isRequired: false)
            },
            new[]
            {
                ScalarField("Name", typeof(string)),
                ReferenceField("Group", typeof(int?), typeof(TenantRow), isRequired: false)
            });

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new UserRow { Id = 7, Name = "Ada", GroupId = 2 },
            isCreate: false,
            errors: new Dictionary<string, string[]>(),
            key: 7,
            fieldOptions: new Dictionary<string, IReadOnlyList<RelatedEntityOption>>
            {
                ["Group"] =
                [
                    new RelatedEntityOption("1", "Admins"),
                    new RelatedEntityOption("2", "Guests", Selected: true)
                ]
            });

        html.Should().Contain("<select class=\"efui-select\" name=\"Group\"");
        html.Should().Contain("<option value=\"1\">Admins</option>");
        html.Should().Contain("<option value=\"2\" selected>Guests</option>");
        html.Should().NotContain("name=\"GroupId\"");
    }

    [Fact]
    public void RenderEditForm_includes_form_theme_stylesheet_semantic_classes_and_breadcrumbs()
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
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string))
            });

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new UserRow { Id = 7, Name = "Ada" },
            isCreate: false,
            errors: new Dictionary<string, string[]>(),
            key: 7);

        html.Should().Contain("href=\"/efui/assets/efui.css\"");
        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/efui\">Efui</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/efui/users\">User</a>");
        html.Should().Contain("<span class=\"efui-breadcrumb-current\">Edit</span>");
        html.Should().Contain("class=\"efui-form\"");
        html.Should().Contain("class=\"efui-field\"");
        html.Should().Contain("class=\"efui-label\"");
        html.Should().Contain("class=\"efui-input\"");
        html.Should().Contain("class=\"efui-button\"");
    }

    [Fact]
    public void RenderEditForm_renders_collection_fields_as_chip_picker()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "Playlist",
            "playlists",
            typeof(TenantRow),
            PrimaryKey("TenantKey", typeof(string)),
            new[]
            {
                AssignedKey("TenantKey", typeof(string)),
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string)),
                CollectionField("Tracks", typeof(TenantRow))
            });

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new TenantRow { TenantKey = "playlist-1", Name = "North" },
            isCreate: false,
            errors: new Dictionary<string, string[]>(),
            key: "playlist-1",
            fieldOptions: new Dictionary<string, IReadOnlyList<RelatedEntityOption>>
            {
                ["Tracks"] =
                [
                    new RelatedEntityOption("1", "Track A", Selected: true),
                    new RelatedEntityOption("2", "Track B")
                ]
            });

        html.Should().Contain("efui-chip-picker");
        html.Should().Contain("efui-chip-picker-selected");
        html.Should().Contain("efui-chip-picker-results");
        html.Should().Contain("efui-chip-picker-fallback");
        html.Should().Contain("data-role=\"chip-picker\"");
        html.Should().Contain("data-role=\"chip-picker-search\"");
        html.Should().Contain("data-role=\"chip-picker-results\"");
        html.Should().Contain("data-role=\"chip-picker-hidden-inputs\"");
        html.Should().Contain("document.addEventListener('DOMContentLoaded'");
        html.Should().Contain("name=\"Tracks\" type=\"checkbox\" value=\"1\" checked");
        html.Should().Contain("name=\"Tracks\" type=\"checkbox\" value=\"2\"");
        html.Should().Contain(">Track A<");
        html.Should().Contain(">Track B<");
        html.Should().NotContain("<select name=\"Tracks\" multiple>");
    }

    [Fact]
    public void RenderEditForm_renders_one_to_many_picker_with_disabled_assigned_elsewhere_options()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "Group",
            "groups",
            typeof(TenantRow),
            PrimaryKey("Id", typeof(int)),
            new[]
            {
                PrimaryKey("Id", typeof(int)),
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string)),
                CollectionField("Users", typeof(TenantRow), CollectionRelationshipKind.OneToMany)
            });

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new GroupRow { Id = 1, Name = "Admins" },
            isCreate: false,
            errors: new Dictionary<string, string[]>(),
            key: 1,
            fieldOptions: new Dictionary<string, IReadOnlyList<RelatedEntityOption>>
            {
                ["Users"] =
                [
                    new RelatedEntityOption("1", "Ada", Selected: true),
                    new RelatedEntityOption("2", "Linus", Selected: false, Disabled: true, Description: "assigned to Guests")
                ]
            });

        html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"1\" checked");
        html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"2\" disabled");
        html.Should().Contain("assigned to Guests");
    }

    [Fact]
    public void RenderEditForm_renders_related_management_links_below_editable_fields()
    {
        var sut = new HtmlPageRenderer();
        var metadata = new EntityMetadata(
            "Invoice",
            "invoices",
            typeof(TenantRow),
            PrimaryKey("Id", typeof(int)),
            new[]
            {
                PrimaryKey("Id", typeof(int)),
                Editable("Name", typeof(string))
            },
            new[]
            {
                Editable("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string))
            },
            new[]
            {
                ScalarField("Name", typeof(string))
            },
            relatedManagementLinks:
            [
                new RelatedEntityManagementLink("InvoiceItems", "invoice_items", typeof(TenantRow), "InvoiceId")
            ]);

        var html = sut.RenderEditForm(
            "/efui",
            metadata,
            new GroupRow { Id = 1, Name = "Invoice 1" },
            isCreate: false,
            errors: new Dictionary<string, string[]>(),
            key: 1);

        html.Should().Contain("Manage related rows");
        html.Should().Contain("/efui/invoice_items?filter.0.field=InvoiceId&filter.0.op=eq&filter.0.value=1");
        html.Should().NotContain("name=\"InvoiceItems\" type=\"checkbox\"");
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
            submittedValues: new Dictionary<string, string[]>
            {
                ["Name"] = ["Edited"],
                ["Email"] = ["edited@example.com"],
                ["CreatedAt"] = ["not-a-date"]
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

        var html = sut.RenderList("/efui", metadata, new RenderedListView(
            new[]
            {
                new RenderedListRow("tenant / north?1", new Dictionary<string, RenderedListCell>
                {
                    ["TenantKey"] = Cell("tenant / north?1"),
                    ["GroupId"] = Cell("Guests"),
                    ["Name"] = Cell("North")
                })
            }));

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

    private static EditableFieldMetadata ScalarField(string name, Type clrType)
        => new(name, EditableFieldKind.Scalar, clrType, name, null, null, false);

    private static EditableFieldMetadata ReferenceField(string name, Type clrType, Type relatedClrType, bool isRequired)
        => new(name, EditableFieldKind.Reference, clrType, $"{name}Id", name, relatedClrType, isRequired);

    private static EditableFieldMetadata CollectionField(string name, Type relatedClrType, CollectionRelationshipKind relationshipKind = CollectionRelationshipKind.ManyToMany)
        => new(name, EditableFieldKind.Collection, typeof(string[]), null, name, relatedClrType, false, relationshipKind);

    private static JsonDocument GetTableConfig(string html)
    {
        var startMarker = "<script type=\"application/json\" data-role=\"efui-table-config\">";
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        start += startMarker.Length;
        var end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);
        return JsonDocument.Parse(html[start..end]);
    }

    private static JsonElement GetColumn(JsonElement config, string field)
        => config.GetProperty("columns")
            .EnumerateArray()
            .Single(column => string.Equals(column.GetProperty("field").GetString(), field, StringComparison.Ordinal));

    private static RenderedListCell Cell(string text, string? href = null)
        => new(text, href);

    private sealed class UserRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public int? GroupId { get; init; }
    }

    private sealed class TenantRow
    {
        public string TenantKey { get; init; } = string.Empty;
        public int GroupId { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class GroupRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
