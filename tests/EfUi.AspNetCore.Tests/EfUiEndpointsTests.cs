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
    public async Task Post_create_user_redirects_back_to_entity_page()
    {
        var response = await _client.PostAsync("/efui/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Grace",
            ["Email"] = "grace@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00",
            ["GroupId"] = "1"
        }));

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/efui/users");
    }

    [Fact]
    public async Task Post_delete_removes_user()
    {
        await _client.PostAsync("/efui/users", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Delete Me",
            ["Email"] = $"delete-{Guid.NewGuid():N}@example.com",
            ["IsActive"] = "true",
            ["CreatedAt"] = "2026-05-17T10:00:00"
        }));

        var html = await _client.GetStringAsync("/efui/users");
        var match = Regex.Match(html, @"<tr>(?:(?!</tr>).)*Delete Me(?:(?!</tr>).)*/efui/users/(?<id>\d+)/edit", RegexOptions.Singleline);
        match.Success.Should().BeTrue();

        var response = await _client.PostAsync($"/efui/users/{match.Groups["id"].Value}/delete", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.IsSuccessStatusCode.Should().BeTrue();
        var updatedHtml = await response.Content.ReadAsStringAsync();
        updatedHtml.Should().NotContain("Delete Me");
    }
}
