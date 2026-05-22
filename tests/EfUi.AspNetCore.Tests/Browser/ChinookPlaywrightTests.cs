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

        await page.GotoAsync("/");
        await page.EvaluateAsync("async () => { await fetch('/auth/edit', { method: 'POST' }); }");
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
