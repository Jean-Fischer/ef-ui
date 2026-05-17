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
    public async Task Get_index_returns_entity_links()
    {
        var html = await _client.GetStringAsync("/efui");

        html.Should().Contain("/efui/users");
        html.Should().Contain("/efui/groups");
    }

    [Fact]
    public async Task Get_entity_page_renders_table()
    {
        var html = await _client.GetStringAsync("/efui/users");

        html.Should().Contain("<table");
        html.Should().Contain("Ada");
    }

    [Fact]
    public async Task Get_new_form_renders_only_editable_fields()
    {
        var html = await _client.GetStringAsync("/efui/users/new");

        html.Should().Contain("name=\"Name\"");
        html.Should().NotContain("name=\"Id\"");
    }

    [Fact]
    public async Task Get_edit_form_for_existing_row_renders_current_values()
    {
        var email = $"edit-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Edit Me", email);

        var html = await _client.GetStringAsync($"/efui/users/{id}/edit");

        html.Should().Contain($"action=\"/efui/users/{id}\"");
        html.Should().Contain("name=\"Name\" value=\"Edit Me\"");
        html.Should().Contain($"name=\"Email\" value=\"{email}\"");
        html.Should().Contain("name=\"IsActive\" value=\"True\"");
        html.Should().NotContain("name=\"Id\"");
    }

    [Fact]
    public async Task Post_create_user_redirects_back_to_entity_page()
    {
        var response = await _client.PostAsync("/efui/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Grace",
            ["Email"] = $"grace-{Guid.NewGuid():N}@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["GroupId"] = "1"
        }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/efui/users");
    }

    [Fact]
    public async Task Post_update_existing_row_redirects_and_persists_changes()
    {
        var originalEmail = $"before-{Guid.NewGuid():N}@example.com";
        var updatedEmail = $"after-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Before Update", originalEmail);

        var response = await _client.PostAsync($"/efui/users/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "After Update",
            ["Email"] = updatedEmail,
            ["IsActive"] = "false",
            ["CreatedAt"] = "2026-05-18T12:30:00",
            ["GroupId"] = "1"
        }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/efui/users");

        var html = await _client.GetStringAsync($"/efui/users/{id}/edit");
        html.Should().Contain("name=\"Name\" value=\"After Update\"");
        html.Should().Contain($"name=\"Email\" value=\"{updatedEmail}\"");
        html.Should().Contain("name=\"IsActive\" value=\"False\"");
        html.Should().Contain("name=\"CreatedAt\" value=\"2026-05-18T12:30:00.0000000\"");
    }

    [Fact]
    public async Task Post_create_failure_preserves_submitted_values()
    {
        var email = $"invalid-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsync("/efui/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Keep Me",
            ["Email"] = email,
            ["IsActive"] = "true",
            ["CreatedAt"] = "not-a-date",
            ["GroupId"] = "1"
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

        var response = await _client.PostAsync($"/efui/users/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
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
    public async Task Post_delete_removes_user()
    {
        var email = $"delete-{Guid.NewGuid():N}@example.com";
        var id = await CreateUserAndGetIdAsync("Delete Me", email);

        var response = await _client.PostAsync($"/efui/users/{id}/delete", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.IsSuccessStatusCode.Should().BeTrue();
        var updatedHtml = await response.Content.ReadAsStringAsync();
        updatedHtml.Should().NotContain(email);
    }

    private async Task<string> CreateUserAndGetIdAsync(string name, string email)
    {
        var createResponse = await _client.PostAsync("/efui/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Email"] = email,
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["GroupId"] = "1"
        }));

        createResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);

        var html = await _client.GetStringAsync("/efui/users");
        var match = Regex.Match(html, $@"<tr>(?:(?!</tr>).)*{Regex.Escape(email)}(?:(?!</tr>).)*/efui/users/(?<id>\d+)/edit", RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        return match.Groups["id"].Value;
    }
}
