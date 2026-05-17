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
    public async Task Sample_host_serves_all_ui_mounts_in_production()
    {
        using var factory = new ProductionEfUiApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var simpleResponse = await client.GetAsync("/simple");
        var simpleHtml = await simpleResponse.Content.ReadAsStringAsync();
        var chinookResponse = await client.GetAsync("/chinook");

        simpleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        simpleHtml.Should().Contain("EF UI");
        chinookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
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
                using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

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
}
