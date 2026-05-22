using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace EfUi.AspNetCore.Tests.Browser;

public sealed class ChinookPlaywrightTests : IAsyncLifetime
{
    private SampleHostProcess? _server;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    [Fact]
    public async Task Playlist_edit_page_hydrates_the_chip_picker_and_supports_browser_interactions()
    {
        var page = _page ?? throw new InvalidOperationException("Playwright page was not initialized.");

        await AuthenticateAsync(page, "Edit");
        await page.GotoAsync("/");
        (await page.GetByText("Current profile: Edit").IsVisibleAsync()).Should().BeTrue();

        var playlistResponse = await page.GotoAsync("/chinook/playlists/1/edit");
        playlistResponse?.Status.Should().Be(200);
        await page.Locator("[data-role='chip-picker'].efui-chip-picker-enhanced").WaitForAsync();
        await page.Locator(".efui-chip-picker-fallback.efui-chip-picker-fallback-hidden").WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        var picker = page.Locator("[data-role='chip-picker']");
        (await picker.GetAttributeAsync("class") ?? string.Empty).Should().Contain("efui-chip-picker-enhanced");
        (await page.Locator(".efui-chip-picker-fallback").IsHiddenAsync()).Should().BeTrue();

        var fallbackCheckboxes = page.Locator(".efui-chip-picker-fallback input[type='checkbox']");
        (await fallbackCheckboxes.CountAsync()).Should().BeGreaterThan(0);
        (await fallbackCheckboxes.First.IsDisabledAsync()).Should().BeTrue();

        var hiddenInputs = page.Locator("[data-role='chip-picker-hidden-inputs'] input[type='hidden']");
        var selectedChips = page.Locator("[data-role='chip-picker-selected'] .efui-chip");
        var availableOptions = page.Locator("[data-role='chip-option']");

        var initialHiddenCount = await hiddenInputs.CountAsync();
        var initialChipCount = await selectedChips.CountAsync();
        var availableCount = await availableOptions.CountAsync();

        availableCount.Should().BeGreaterThan(0);

        var firstOption = availableOptions.First;
        var firstValue = await firstOption.GetAttributeAsync("data-value");
        firstValue.Should().NotBeNullOrWhiteSpace();

        await firstOption.ClickAsync();
        await page.Locator($"[data-role='chip-remove'][data-value='{firstValue}']").WaitForAsync();

        (await hiddenInputs.CountAsync()).Should().Be(initialHiddenCount + 1);
        (await selectedChips.CountAsync()).Should().Be(initialChipCount + 1);

        await page.Locator($"[data-role='chip-remove'][data-value='{firstValue}']").ClickAsync();

        (await hiddenInputs.CountAsync()).Should().Be(initialHiddenCount);
        (await selectedChips.CountAsync()).Should().Be(initialChipCount);
    }

    [Fact]
    public async Task Playlist_crud_flow_respects_anonymous_and_readonly_access_before_creating_editing_and_deleting()
    {
        var page = _page ?? throw new InvalidOperationException("Playwright page was not initialized.");

        await AuthenticateAsync(page, "Anonymous");
        var anonymousStatus = await page.EvaluateAsync<int>("async () => { const response = await fetch('/chinook'); return response.status; }");
        anonymousStatus.Should().Be(401);

        await AuthenticateAsync(page, "ReadOnly");
        var readonlyResponse = await page.GotoAsync("/chinook/playlists");
        readonlyResponse?.Status.Should().Be(200);
        (await page.Locator("a.efui-primary-link[href='/chinook/playlists/new']").CountAsync()).Should().Be(0);
        (await page.Locator(".efui-row-actions").CountAsync()).Should().Be(0);

        var readonlyMutationStatus = await page.EvaluateAsync<int>("async () => { const response = await fetch('/chinook/playlists', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'Name=ShouldFail' }); return response.status; }");
        readonlyMutationStatus.Should().Be(403);

        await AuthenticateAsync(page, "Edit");
        var uniqueSuffix = Guid.NewGuid().ToString("N");
        var playlistName = $"Playwright Playlist {uniqueSuffix}";
        var updatedPlaylistName = $"Playwright Playlist Updated {uniqueSuffix}";

        var createResponse = await page.GotoAsync("/chinook/playlists/new");
        createResponse?.Status.Should().Be(200);
        await page.Locator("input[name='Name']").FillAsync(playlistName);
        await page.Locator("form.efui-form button.efui-button[type='submit']").ClickAsync();
        await page.WaitForURLAsync("**/chinook/playlists");

        var createdRow = page.Locator("table.efui-table tbody tr", new() { HasText = playlistName });
        await createdRow.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        (await createdRow.CountAsync()).Should().Be(1);
        var editUrl = await createdRow.Locator("a.efui-row-action-link").GetAttributeAsync("href");
        editUrl.Should().NotBeNullOrWhiteSpace();

        var editResponse = await page.GotoAsync(editUrl!);
        editResponse?.Status.Should().Be(200);
        await page.Locator("input[name='Name']").FillAsync(updatedPlaylistName);
        await page.Locator("form.efui-form button.efui-button[type='submit']").ClickAsync();
        await page.WaitForURLAsync("**/chinook/playlists");

        var updatedRow = page.Locator("table.efui-table tbody tr", new() { HasText = updatedPlaylistName });
        await updatedRow.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        (await updatedRow.CountAsync()).Should().Be(1);
        (await page.Locator("table.efui-table tbody tr", new() { HasText = playlistName }).CountAsync()).Should().Be(0);

        var deleteUrl = await updatedRow.Locator("form.efui-row-action-form").GetAttributeAsync("action");
        deleteUrl.Should().NotBeNullOrWhiteSpace();

        var deleteSucceeded = await page.EvaluateAsync<bool>($"async () => {{ const response = await fetch('{deleteUrl}', {{ method: 'POST' }}); return response.ok; }}");
        deleteSucceeded.Should().BeTrue();

        var listResponse = await page.GotoAsync("/chinook/playlists");
        listResponse?.Status.Should().Be(200);
        (await page.Locator("table.efui-table tbody tr", new() { HasText = updatedPlaylistName }).CountAsync()).Should().Be(0);
    }

    private static async Task AuthenticateAsync(IPage page, string role)
    {
        var endpoint = role switch
        {
            "Anonymous" => "/auth/anonymous",
            "ReadOnly" => "/auth/readonly",
            "Edit" => "/auth/edit",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported authentication role")
        };

        await page.GotoAsync("/");
        await page.EvaluateAsync($"async () => {{ await fetch('{endpoint}', {{ method: 'POST' }}); }}");
    }

    public async Task InitializeAsync()
    {
        _server = await SampleHostProcess.StartAsync();
        _playwright = await Playwright.CreateAsync();
        _browser = await LaunchBrowserAsync(_playwright);
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _server.BaseUri.ToString()
        });
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }

        if (_context is not null)
        {
            await _context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Channel = "msedge"
            });
        }
        catch (PlaywrightException)
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        }
    }
}
