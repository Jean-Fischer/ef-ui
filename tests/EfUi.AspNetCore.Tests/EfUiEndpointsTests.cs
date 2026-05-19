using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EfUi.SampleHost.Data;
using SampleGroup = EfUi.SampleHost.Models.Group;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public class EfUiEndpointsTests : IClassFixture<EfUiApplicationFactory>
{
    private readonly EfUiApplicationFactory _factory;
    private readonly HttpClient _client;

    public EfUiEndpointsTests(EfUiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_root_returns_links_to_all_ui_mounts()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("/simple");
        html.Should().Contain("/chinook");
    }

    [Fact]
    public async Task Get_index_returns_entity_links_with_themed_shell_and_breadcrumbs()
    {
        var html = await _client.GetStringAsync("/simple");

        html.Should().Contain("href=\"/simple/assets/efui.css\"");
        html.Should().Contain("class=\"efui-body\"");
        html.Should().Contain("<main class=\"efui-page\">");
        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<span class=\"efui-breadcrumb-current\">Simple</span>");
        html.Should().Contain("<section class=\"efui-surface\">");
        html.Should().Contain("<ul class=\"efui-index-list efui-link-grid\">");
        html.Should().Contain("/simple/users");
        html.Should().Contain("/simple/groups");
    }

    [Fact]
    public async Task Get_stylesheet_includes_readonly_page_theme_classes()
    {
        var css = await _client.GetStringAsync("/simple/assets/efui.css");

        css.Should().Contain(".efui-link-grid");
        css.Should().Contain(".efui-breadcrumbs");
        css.Should().Contain(".efui-table-status");
        css.Should().Contain(".efui-page-actions");
        css.Should().Contain(".efui-primary-link");
        css.Should().Contain(".efui-table-wrapper");
        css.Should().Contain(".efui-row-actions");
        css.Should().Contain(".efui-row-action-button");
    }

    [Fact]
    public async Task Get_table_enhancement_assets_expose_tabulator_bootstrap_shell()
    {
        var script = await _client.GetStringAsync("/simple/assets/efui-table.js");
        var css = await _client.GetStringAsync("/simple/assets/efui-table.css");

        script.Should().Contain("Tabulator");
        script.Should().Contain("efui-table-config");
        script.Should().Contain("URLSearchParams");
        script.Should().Contain("window.location");
        script.Should().Contain("efui-table-loading");
        script.Should().NotContain("efui-query-form");
        css.Should().Contain(".efui-table-enhancement");
        css.Should().Contain(".efui-table-loading");
    }

    [Fact]
    public async Task Get_entity_page_renders_themed_table_with_related_labels_and_fk_links()
    {
        var adminsEmail = $"related-label-admins-{Guid.NewGuid():N}@example.com";
        var guestsEmail = $"related-label-guests-{Guid.NewGuid():N}@example.com";

        await CreateUserAndGetIdAsync("Related Label Admin", adminsEmail, group: "1");
        await CreateUserAndGetIdAsync("Related Label Guest", guestsEmail, group: "2");

        var html = await _client.GetStringAsync("/simple/users");
        var adminsRow = GetTableRowContainingValue(html, adminsEmail);
        var guestsRow = GetTableRowContainingValue(html, guestsEmail);

        html.Should().Contain("href=\"/simple/assets/efui.css\"");
        html.Should().Contain("class=\"efui-body\"");
        html.Should().Contain("<main class=\"efui-page\">");
        html.Should().Contain("<section class=\"efui-surface\">");
        html.Should().Contain("<div class=\"efui-page-actions\">");
        html.Should().Contain("<a class=\"efui-primary-link\" href=\"/simple/users/new\">Create New</a>");
        html.Should().Contain("<div class=\"efui-table-wrapper\" data-role=\"efui-table-fallback\">");
        html.Should().Contain("<table class=\"efui-table\">");
        html.Should().Contain("class=\"efui-row-actions\"");
        html.Should().Contain("class=\"efui-row-action-link\"");
        html.Should().Contain("class=\"efui-row-action-button\"");
        adminsRow.Should().Contain($"<td>{adminsEmail}</td><td><a class=\"efui-cell-link\" href=\"/simple/groups/1/edit\">Admins</a></td><td>True</td><td>Related Label Admin</td>");
        adminsRow.Should().NotContain($"<td>{adminsEmail}</td><td>1</td><td>True</td><td>Related Label Admin</td>");
        guestsRow.Should().Contain($"<td>{guestsEmail}</td><td><a class=\"efui-cell-link\" href=\"/simple/groups/2/edit\">Guests</a></td><td>True</td><td>Related Label Guest</td>");
        guestsRow.Should().NotContain($"<td>{guestsEmail}</td><td>2</td><td>True</td><td>Related Label Guest</td>");
    }

    [Fact]
    public async Task Get_entity_page_shows_raw_foreign_key_value_when_related_row_is_missing()
    {
        var email = $"missing-related-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Missing Related Label User", email, group: "1");

        await SetUserGroupIdWithoutRelatedRowAsync(id, 999999);

        var html = await _client.GetStringAsync("/simple/users");
        var row = GetTableRowContainingValue(html, email);

        row.Should().Contain($"<td>{email}</td><td>999999</td><td>True</td><td>Missing Related Label User</td>");
        row.Should().NotContain("/simple/groups/999999/edit");
        row.Should().NotContain("<td>Admins</td>");
        row.Should().NotContain("<td>Guests</td>");
    }

    [Fact]
    public async Task Get_entity_page_renders_null_foreign_key_as_empty()
    {
        var email = $"null-related-{Guid.NewGuid():N}@example.com";
        await CreateUserAndGetIdAsync("Null Group User", email, group: null);

        var html = await _client.GetStringAsync("/simple/users");
        var row = GetTableRowContainingValue(html, email);

        row.Should().Contain($"<td>{email}</td><td></td><td>True</td><td>Null Group User</td>");
        row.Should().NotContain("<td>Admins</td>");
        row.Should().NotContain("<td>Guests</td>");
    }

    [Fact]
    public async Task Get_entity_page_renders_active_query_state_from_url_in_compact_status_area()
    {
        var html = await _client.GetStringAsync("/simple/users?filter.0.field=Name&filter.0.op=contains&filter.0.value=Ada&sort.0.field=Email&sort.0.dir=desc&offset=0&limit=25");

        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/simple\">Simple</a>");
        html.Should().Contain("<span class=\"efui-breadcrumb-current\">User</span>");
        html.Should().Contain("class=\"efui-table-status\"");
        html.Should().Contain("Name contains Ada");
        html.Should().Contain("Email desc");
        html.Should().NotContain("class=\"efui-query-builder\"");
        html.Should().NotContain("efui-query-builder-form");
        html.Should().NotContain("data-role=\"efui-query-form\"");
        html.Should().Contain("href=\"/simple/assets/efui-table.css\"");
        html.Should().Contain("src=\"/simple/assets/efui-table.js\"");
        html.Should().Contain("data-role=\"efui-table-enhancement\"");
        html.Should().Contain("data-role=\"efui-table-config\"");
        html.Should().Contain("data-role=\"efui-table-loading\"");
        html.Should().Contain("data-role=\"efui-table-fallback\"");
        html.Should().Contain("\"library\":\"tabulator\"");
        html.Should().Contain("\"field\":\"__actions\"");

        using var config = GetTableConfig(html);
        var root = config.RootElement;
        root.GetProperty("listUrl").GetString().Should().Be("/simple/users");

        var nameColumn = GetColumn(root, "Name");
        nameColumn.GetProperty("headerSort").GetBoolean().Should().BeTrue();
        nameColumn.GetProperty("headerFilter").GetString().Should().Be("input");
        nameColumn.GetProperty("filterOperator").GetString().Should().Be("contains");
        nameColumn.GetProperty("headerFilterValue").GetString().Should().Be("Ada");
        nameColumn.GetProperty("sortDirection").ValueKind.Should().Be(JsonValueKind.Null);

        var emailColumn = GetColumn(root, "Email");
        emailColumn.GetProperty("sortDirection").GetString().Should().Be("desc");

        var actionsColumn = GetColumn(root, "__actions");
        actionsColumn.GetProperty("sortDirection").ValueKind.Should().Be(JsonValueKind.Null);
        actionsColumn.GetProperty("headerSort").GetBoolean().Should().BeFalse();
        actionsColumn.GetProperty("headerFilter").ValueKind.Should().Be(JsonValueKind.False);
        actionsColumn.GetProperty("filterOperator").ValueKind.Should().Be(JsonValueKind.Null);

        var activeSort = root.GetProperty("query").GetProperty("sorts").EnumerateArray().Single();
        activeSort.GetProperty("Field").GetString().Should().Be("Email");
        activeSort.GetProperty("Direction").GetString().Should().Be("desc");
    }

    [Fact]
    public async Task Get_entity_page_surfaces_invalid_query_builder_rules_as_visible_errors()
    {
        var html = await _client.GetStringAsync("/simple/users?filter.0.field=DropTable&filter.0.op=contains&filter.0.value=Ada&sort.0.field=Email&sort.0.dir=sideways");

        html.Should().Contain("class=\"efui-error-summary\"");
        html.Should().Contain("Unsupported filter field");
        html.Should().Contain("DropTable");
        html.Should().Contain("Unsupported sort direction");
        html.Should().Contain("sideways");
    }

    [Fact]
    public async Task Get_entity_page_filters_rows_from_scalar_query_rules()
    {
        var includedEmail = $"filter-include-{Guid.NewGuid():N}@example.com";
        var excludedEmail = $"filter-exclude-{Guid.NewGuid():N}@example.com";

        await CreateUserAndGetIdAsync("Filter Included User", includedEmail);
        await CreateUserAndGetIdAsync("Hidden User", excludedEmail);

        var html = await _client.GetStringAsync("/simple/users?filter.0.field=Name&filter.0.op=contains&filter.0.value=Included");

        html.Should().Contain(includedEmail);
        html.Should().NotContain(excludedEmail);
        html.Should().Contain("Name contains Included");
    }

    [Fact]
    public async Task Get_entity_page_sorts_rows_from_scalar_query_rules()
    {
        var earlierEmail = $"sort-a-{Guid.NewGuid():N}@example.com";
        var laterEmail = $"sort-z-{Guid.NewGuid():N}@example.com";

        await CreateUserAndGetIdAsync("Sort A User", earlierEmail);
        await CreateUserAndGetIdAsync("Sort Z User", laterEmail);

        var html = await _client.GetStringAsync("/simple/users?sort.0.field=Email&sort.0.dir=desc");

        html.IndexOf(laterEmail, StringComparison.Ordinal).Should().BeLessThan(html.IndexOf(earlierEmail, StringComparison.Ordinal));
        html.Should().Contain("Email desc");
    }

    [Fact]
    public async Task Get_entity_page_filters_fk_display_columns_by_label_semantics()
    {
        var adminsEmail = $"label-admins-{Guid.NewGuid():N}@example.com";
        var guestsEmail = $"label-guests-{Guid.NewGuid():N}@example.com";

        await CreateUserAndGetIdAsync("Label Admin User", adminsEmail, group: "1");
        await CreateUserAndGetIdAsync("Label Guest User", guestsEmail, group: "2");

        var html = await _client.GetStringAsync("/simple/users?filter.0.field=GroupId&filter.0.op=contains&filter.0.value=Guest");

        html.Should().Contain(guestsEmail);
        html.Should().NotContain(adminsEmail);
        html.Should().Contain("GroupId contains Guest");
    }

    [Fact]
    public async Task Get_entity_page_sorts_fk_display_columns_by_label_semantics()
    {
        var zuluGroupId = await CreateGroupAsync("Zulu Squad");
        var alphaGroupId = await CreateGroupAsync("Alpha Squad");
        var zuluEmail = $"label-zulu-{Guid.NewGuid():N}@example.com";
        var alphaEmail = $"label-alpha-{Guid.NewGuid():N}@example.com";

        await CreateUserAndGetIdAsync("Zulu User", zuluEmail, group: zuluGroupId.ToString());
        await CreateUserAndGetIdAsync("Alpha User", alphaEmail, group: alphaGroupId.ToString());

        var html = await _client.GetStringAsync("/simple/users?sort.0.field=GroupId&sort.0.dir=asc");

        html.IndexOf(alphaEmail, StringComparison.Ordinal).Should().BeLessThan(html.IndexOf(zuluEmail, StringComparison.Ordinal));
        html.Should().Contain("GroupId asc");
    }

    [Fact]
    public async Task Get_new_form_renders_only_editable_fields_and_breadcrumbs()
    {
        var html = await _client.GetStringAsync("/simple/users/new");

        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/simple\">Simple</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/simple/users\">User</a>");
        html.Should().Contain("<span class=\"efui-breadcrumb-current\">New</span>");
        html.Should().Contain("name=\"Name\"");
        html.Should().NotContain("name=\"Id\"");
    }

    [Fact]
    public async Task Get_group_create_form_does_not_render_one_to_many_picker_before_parent_exists()
    {
        var html = await _client.GetStringAsync("/simple/groups/new");

        html.Should().NotContain("name=\"Users\" type=\"checkbox\"");
    }

    [Fact]
    public async Task Get_edit_form_for_existing_row_renders_current_values_and_breadcrumbs()
    {
        var email = $"edit-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Edit Me", email);

        var html = await _client.GetStringAsync($"/simple/users/{id}/edit");

        html.Should().Contain("<nav class=\"efui-breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/\">EF UI</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/simple\">Simple</a>");
        html.Should().Contain("<a class=\"efui-breadcrumb-link\" href=\"/simple/users\">User</a>");
        html.Should().Contain("<span class=\"efui-breadcrumb-current\">Edit</span>");
        html.Should().Contain($"action=\"/simple/users/{id}\"");
        html.Should().Contain("name=\"Name\" value=\"Edit Me\"");
        html.Should().Contain($"name=\"Email\" value=\"{email}\"");
        html.Should().Contain("name=\"IsActive\" value=\"True\"");
        html.Should().NotContain("name=\"Id\"");
    }

    [Fact]
    public async Task Get_edit_form_renders_group_dropdown_instead_of_raw_group_id_input()
    {
        var email = $"group-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Group User", email);

        var html = await _client.GetStringAsync($"/simple/users/{id}/edit");

        html.Should().Contain("<select class=\"efui-select\" name=\"Group\">");
        html.Should().Contain(">Admins<");
        html.Should().Contain(">Guests<");
        html.Should().NotContain("name=\"GroupId\"");
    }

    [Fact]
    public async Task Get_group_edit_form_renders_users_as_one_to_many_picker_with_disabled_foreign_owned_rows()
    {
        var email = $"group-picker-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Group Picker User", email);

        var html = await _client.GetStringAsync("/simple/groups/1/edit");

        html.Should().Contain("type=\"search\"");
        html.Should().Contain($"name=\"Users\" type=\"checkbox\" value=\"{id}\" checked");
        html.Should().Contain("name=\"Users\" type=\"checkbox\" value=\"2\" disabled");
        html.Should().Contain("assigned to Guests");
        html.Should().NotContain("name=\"GroupId\"");
    }

    [Fact]
    public async Task Post_create_user_redirects_back_to_entity_page()
    {
        var response = await _client.PostAsync("/simple/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Grace",
            ["Email"] = $"grace-{Guid.NewGuid():N}@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["Group"] = "1"
        }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/simple/users");
    }

    [Fact]
    public async Task Post_update_existing_row_redirects_and_persists_changes()
    {
        var originalEmail = $"before-{Guid.NewGuid():N}@example.com";
        var updatedEmail = $"after-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Before Update", originalEmail);

        var response = await _client.PostAsync($"/simple/users/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "After Update",
            ["Email"] = updatedEmail,
            ["IsActive"] = "false",
            ["CreatedAt"] = "2026-05-18T12:30:00",
            ["Group"] = "2"
        }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/simple/users");

        var html = await _client.GetStringAsync($"/simple/users/{id}/edit");
        html.Should().Contain("name=\"Name\" value=\"After Update\"");
        html.Should().Contain($"name=\"Email\" value=\"{updatedEmail}\"");
        html.Should().Contain("name=\"IsActive\" value=\"False\"");
        html.Should().Contain("name=\"CreatedAt\" value=\"2026-05-18T12:30:00.0000000\"");
        html.Should().Contain("<option value=\"2\" selected>Guests</option>");
    }

    [Fact]
    public async Task Post_create_failure_preserves_submitted_values()
    {
        var email = $"invalid-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsync("/simple/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Keep Me",
            ["Email"] = email,
            ["IsActive"] = "true",
            ["CreatedAt"] = "not-a-date",
            ["Group"] = "1"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("name=\"Name\" value=\"Keep Me\"");
        html.Should().Contain($"name=\"Email\" value=\"{email}\"");
        html.Should().Contain("name=\"CreatedAt\" value=\"not-a-date\"");
    }

    [Fact]
    public async Task Post_update_failure_preserves_submitted_values_without_showing_stale_model_values()
    {
        var originalEmail = $"original-{Guid.NewGuid():N}@example.com";
        var submittedEmail = $"submitted-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Original Name", originalEmail);

        var response = await _client.PostAsync($"/simple/users/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Edited Name",
            ["Email"] = submittedEmail,
            ["IsActive"] = "false",
            ["CreatedAt"] = "bad-date"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("name=\"Name\" value=\"Edited Name\"");
        html.Should().Contain($"name=\"Email\" value=\"{submittedEmail}\"");
        html.Should().Contain("name=\"IsActive\" value=\"false\"");
        html.Should().Contain("name=\"CreatedAt\" value=\"bad-date\"");
        html.Should().NotContain(originalEmail);
        html.Should().NotContain("2026-05-17T10:00:00.0000000");
    }

    [Fact]
    public async Task Post_update_group_with_no_users_clears_optional_one_to_many_assignments()
    {
        var email = $"clear-group-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Clear Group User", email);

        var response = await _client.PostAsync(
            "/simple/groups/1",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Admins")
            }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);

        var html = await _client.GetStringAsync("/simple/groups/1/edit");
        html.Should().NotContain($"name=\"Users\" type=\"checkbox\" value=\"{id}\" checked");
    }

    [Fact]
    public async Task Post_delete_removes_user()
    {
        var email = $"delete-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Delete Me", email);

        var response = await _client.PostAsync($"/simple/users/{id}/delete", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.IsSuccessStatusCode.Should().BeTrue();
        var updatedHtml = await response.Content.ReadAsStringAsync();
        updatedHtml.Should().NotContain(email);
    }

    [Fact]
    public async Task Post_delete_missing_row_returns_not_found()
    {
        var response = await _client.PostAsync("/simple/users/999999/delete", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> CreateGroupAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var group = new SampleGroup { Name = name };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    private async Task<string> CreateUserAndGetIdAsync(string name, string email, string? group = "1")
    {
        var values = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Email"] = email,
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00"
        };

        if (group is not null)
        {
            values["Group"] = group;
        }

        var createResponse = await _client.PostAsync("/simple/users", new FormUrlEncodedContent(values));

        createResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);

        var html = await _client.GetStringAsync("/simple/users");
        var row = GetTableRowContainingValue(html, email);
        var match = Regex.Match(row, @"/simple/users/(?<id>\d+)/edit");
        match.Success.Should().BeTrue();
        return match.Groups["id"].Value;
    }

    private async Task SetUserGroupIdWithoutRelatedRowAsync(string userId, int missingGroupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var parsedUserId = int.Parse(userId);

        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Users SET GroupId = {missingGroupId} WHERE Id = {parsedUserId};");
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

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

    private static string GetTableRowContainingValue(string html, string value)
    {
        var match = Regex.Match(html, $@"<tr>(?:(?!</tr>).)*{Regex.Escape(value)}(?:(?!</tr>).)*</tr>", RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        return match.Value;
    }
}
