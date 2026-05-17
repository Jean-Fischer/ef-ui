using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public sealed class ChinookEndpointsTests : IClassFixture<EfUiApplicationFactory>
{
    private readonly HttpClient _client;

    public ChinookEndpointsTests(EfUiApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_chinook_index_returns_entity_links()
    {
        var html = await _client.GetStringAsync("/chinook");

        html.Should().Contain("/chinook/genres");
        html.Should().Contain("/chinook/media_types");
    }

    [Fact]
    public async Task Get_chinook_index_hides_internal_tables()
    {
        var html = await _client.GetStringAsync("/chinook");

        html.Should().NotContain("/chinook/flyway_schema_history");
    }

    [Fact]
    public async Task Get_playlist_edit_form_renders_tracks_multi_select()
    {
        var html = await _client.GetStringAsync("/chinook/playlists/1/edit");

        html.Should().Contain("<select name=\"Tracks\" multiple>");
        html.Should().Contain("Track");
    }

    [Fact]
    public async Task Post_update_playlist_reconciles_track_selection()
    {
        var response = await _client.PostAsync(
            "/chinook/playlists/1",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Music"),
                new KeyValuePair<string, string>("Tracks", "2"),
                new KeyValuePair<string, string>("Tracks", "3")
            }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/chinook/playlists");

        var html = await _client.GetStringAsync("/chinook/playlists/1/edit");
        html.Should().Contain("<option value=\"2\" selected");
        html.Should().Contain("<option value=\"3\" selected");
    }

    [Fact]
    public async Task Post_update_genre_persists_changes()
    {
        var updatedName = $"Updated Genre {Guid.NewGuid():N}";

        var response = await _client.PostAsync("/chinook/genres/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = updatedName
        }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/chinook/genres");

        var html = await _client.GetStringAsync("/chinook/genres/1/edit");
        html.Should().Contain($"name=\"Name\" value=\"{updatedName}\"");
    }
}
