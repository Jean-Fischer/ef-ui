using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public sealed class EfUiProductionTests
{
    [Fact]
    public async Task Sample_host_serves_the_open_mount_and_protects_chinook_in_production()
    {
        using var factory = new ProductionEfUiApplicationFactory();
        using var client = CreateClient(factory);

        var simpleResponse = await client.GetAsync("/simple");
        var simpleHtml = await simpleResponse.Content.ReadAsStringAsync();
        var chinookResponse = await client.GetAsync("/chinook");

        simpleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        simpleHtml.Should().Contain("EF UI");
        chinookResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticated_users_can_access_the_protected_chinook_mount_in_production()
    {
        using var factory = new ProductionEfUiApplicationFactory();
        using var client = CreateClient(factory);

        await AuthenticateAsync(client, "Edit");

        var chinookResponse = await client.GetAsync("/chinook");
        var chinookHtml = await chinookResponse.Content.ReadAsStringAsync();

        chinookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        chinookHtml.Should().Contain("Chinook");
    }

    [Fact]
    public void Factories_use_unique_temp_database_paths_for_both_sample_and_chinook_databases()
    {
        using var firstFactory = new EfUiApplicationFactory();
        using var secondFactory = new EfUiApplicationFactory();

        _ = firstFactory.CreateClient();
        _ = secondFactory.CreateClient();

        var firstConfig = firstFactory.Services.GetRequiredService<IConfiguration>();
        var secondConfig = secondFactory.Services.GetRequiredService<IConfiguration>();

        var firstSample = firstConfig.GetConnectionString("Sample");
        var secondSample = secondConfig.GetConnectionString("Sample");
        var firstChinook = firstConfig.GetConnectionString("Chinook");
        var secondChinook = secondConfig.GetConnectionString("Chinook");

        firstSample.Should().NotBeNullOrWhiteSpace();
        secondSample.Should().NotBeNullOrWhiteSpace();
        firstChinook.Should().NotBeNullOrWhiteSpace();
        secondChinook.Should().NotBeNullOrWhiteSpace();

        firstSample.Should().NotBe(secondSample);
        firstChinook.Should().NotBe(secondChinook);
    }

    [Fact]
    public async Task Multiple_factories_can_boot_in_parallel_without_database_races()
    {
        var factories = Enumerable.Range(0, 6)
            .Select(_ => new EfUiApplicationFactory())
            .ToList();

        try
        {
            var tasks = factories.Select(async factory =>
            {
                using var client = CreateClient(factory);

                var response = await client.GetAsync("/simple");
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            });

            await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var factory in factories)
            {
                factory.Dispose();
            }
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task AuthenticateAsync(HttpClient client, string role)
    {
        var endpoint = role.Equals("Edit", StringComparison.OrdinalIgnoreCase)
            ? "/auth/edit"
            : "/auth/readonly";

        var response = await client.PostAsync(endpoint, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/");
    }
}
