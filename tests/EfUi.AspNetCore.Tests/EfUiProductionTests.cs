using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
