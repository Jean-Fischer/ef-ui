using System.Net;
using System.Text.RegularExpressions;
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
    public async Task Get_chinook_index_returns_entity_links_with_themed_shell()
    {
        var html = await _client.GetStringAsync("/chinook");

        html.Should().Contain("href=\"/chinook/assets/efui.css\"");
        html.Should().Contain("<main class=\"efui-page\">");
        html.Should().Contain("<section class=\"efui-surface\">");
        html.Should().Contain("<ul class=\"efui-index-list efui-link-grid\">");
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
    public async Task Get_albums_list_uses_related_artist_labels_with_themed_table_markup_and_fk_links()
    {
        var html = await _client.GetStringAsync("/chinook/albums");

        html.Should().Contain("<div class=\"efui-page-actions\">");
        html.Should().Contain("<a class=\"efui-primary-link\" href=\"/chinook/albums/new\">Create New</a>");
        html.Should().Contain("<div class=\"efui-table-wrapper\">");
        html.Should().Contain("<table class=\"efui-table\">");
        html.Should().Contain("class=\"efui-row-actions\"");
        html.Should().Contain("class=\"efui-row-action-link\"");
        html.Should().Contain("class=\"efui-row-action-button\"");
        Regex.IsMatch(html, @"<tr><td>1</td><td><a class=\""efui-cell-link\"" href=\""/chinook/artists/1/edit\"">AC/DC</a></td><td>For Those About To Rock We Salute You</td>", RegexOptions.Singleline).Should().BeTrue();
        Regex.IsMatch(html, @"<tr><td>1</td><td>1</td><td>For Those About To Rock We Salute You</td>", RegexOptions.Singleline).Should().BeFalse();
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
    public async Task Get_invoice_edit_form_shows_manage_link_with_prefilter_for_payload_join_rows()
    {
        var html = await _client.GetStringAsync("/chinook/invoices/1/edit");

        html.Should().Contain("Manage related rows");
        html.Should().Contain("/chinook/invoice_items?filter.0.field=InvoiceId&filter.0.op=eq&filter.0.value=1");
        html.Should().NotContain("name=\"InvoiceItems\" type=\"checkbox\"");
    }

    [Fact]
    public async Task Get_invoice_items_list_shows_prefilter_from_related_rows_link_as_visible_query_state()
    {
        var html = await _client.GetStringAsync("/chinook/invoice_items?filter.0.field=InvoiceId&filter.0.op=eq&filter.0.value=1");

        html.Should().Contain("InvoiceId eq 1");
        html.Should().Contain("class=\"efui-query-builder\"");
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
