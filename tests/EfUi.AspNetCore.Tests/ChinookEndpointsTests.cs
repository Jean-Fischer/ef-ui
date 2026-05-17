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
    public async Task Get_playlist_edit_form_renders_tracks_chip_picker_shell()
    {
        var html = await _client.GetStringAsync("/chinook/playlists/1/edit");

        html.Should().Contain("efui-chip-picker");
        html.Should().Contain("efui-chip-picker-fallback");
        html.Should().Contain("data-role=\"chip-picker-search\"");
        html.Should().Contain("name=\"Tracks\" type=\"checkbox\"");
        html.Should().Contain("Track");
        html.Should().NotContain("<select name=\"Tracks\" multiple>");
    }

    [Fact]
    public async Task Get_playlist_edit_form_links_local_form_stylesheet()
    {
        var html = await _client.GetStringAsync("/chinook/playlists/1/edit");

        html.Should().Contain("href=\"/chinook/assets/efui.css\"");
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
        html.Should().Contain("name=\"Tracks\" type=\"checkbox\" value=\"2\" checked");
        html.Should().Contain("name=\"Tracks\" type=\"checkbox\" value=\"3\" checked");
    }

    [Fact]
    public async Task Post_update_playlist_with_no_tracks_clears_track_selection()
    {
        var response = await _client.PostAsync(
            "/chinook/playlists/1",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Music")
            }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/chinook/playlists");

        var html = await _client.GetStringAsync("/chinook/playlists/1/edit");
        html.Should().NotContain("name=\"Tracks\" type=\"checkbox\" value=\"2\" checked");
        html.Should().NotContain("name=\"Tracks\" type=\"checkbox\" value=\"3\" checked");
    }

    [Fact]
    public async Task Post_update_artist_with_no_albums_returns_required_relationship_validation_error()
    {
        var response = await _client.PostAsync(
            "/chinook/artists/1",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "AC/DC")
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Albums");
        html.Should().Contain("cannot be removed");
    }

    [Fact]
    public async Task Get_invoice_edit_form_shows_manage_link_for_payload_join_rows()
    {
        var html = await _client.GetStringAsync("/chinook/invoices/1/edit");

        html.Should().Contain("Manage related rows");
        html.Should().Contain("/chinook/invoice_items");
        html.Should().NotContain("name=\"InvoiceItems\" type=\"checkbox\"");
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
