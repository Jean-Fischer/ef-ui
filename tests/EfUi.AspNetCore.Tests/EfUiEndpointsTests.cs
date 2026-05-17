using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public class EfUiEndpointsTests : IClassFixture<EfUiApplicationFactory>
{
    private readonly HttpClient _client;

    public EfUiEndpointsTests(EfUiApplicationFactory factory)
    {
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
    public async Task Get_index_returns_entity_links()
    {
        var html = await _client.GetStringAsync("/simple");

        html.Should().Contain("/simple/users");
        html.Should().Contain("/simple/groups");
    }

    [Fact]
    public async Task Get_entity_page_renders_table()
    {
        var html = await _client.GetStringAsync("/simple/users");

        html.Should().Contain("<table");
        html.Should().Contain("Ada");
    }

    [Fact]
    public async Task Get_new_form_renders_only_editable_fields()
    {
        var html = await _client.GetStringAsync("/simple/users/new");

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
    public async Task Get_edit_form_for_existing_row_renders_current_values()
    {
        var email = $"edit-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Edit Me", email);

        var html = await _client.GetStringAsync($"/simple/users/{id}/edit");

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

        html.Should().Contain("<select name=\"Group\">");
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

    private async Task<string> CreateUserAndGetIdAsync(string name, string email)
    {
        var createResponse = await _client.PostAsync("/simple/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Email"] = email,
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["Group"] = "1"
        }));

        createResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);

        var html = await _client.GetStringAsync("/simple/users");
        var match = Regex.Match(html, $@"<tr>(?:(?!</tr>).)*{Regex.Escape(email)}(?:(?!</tr>).)*/simple/users/(?<id>\d+)/edit", RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        return match.Groups["id"].Value;
    }
}
