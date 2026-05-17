using EfUi.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EfUi.AspNetCore.Tests;

public sealed class EscapedStringKeyRoutingTests
{
    [Fact]
    public async Task Get_create_form_and_post_create_support_assigned_string_keys()
    {
        await using var host = await StringKeyEfUiTestHost.CreateAsync();

        var createHtml = await host.Client.GetStringAsync("/efui/tenants/new");
        createHtml.Should().Contain("name=\"TenantKey\"");
        createHtml.Should().Contain("name=\"Name\"");

        var response = await host.Client.PostAsync("/efui/tenants", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TenantKey"] = "tenant south?2",
            ["Name"] = "South"
        }));

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/efui/tenants");

        var listHtml = await host.Client.GetStringAsync("/efui/tenants");
        listHtml.Should().Contain("South");
        listHtml.Should().Contain("/efui/tenants/tenant%20south%3F2/edit");
    }

    [Fact]
    public async Task Post_create_duplicate_assigned_string_key_returns_bad_request_with_preserved_values()
    {
        await using var host = await StringKeyEfUiTestHost.CreateAsync();

        var response = await host.Client.PostAsync("/efui/tenants", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TenantKey"] = "tenant north?1",
            ["Name"] = "Duplicate North"
        }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Could not save changes.");
        html.Should().Contain("name=\"TenantKey\" value=\"tenant north?1\"");
        html.Should().Contain("name=\"Name\" value=\"Duplicate North\"");
    }

    [Fact]
    public async Task Post_update_with_escaped_string_key_survives_routing_and_binding_without_showing_key_as_editable()
    {
        await using var host = await StringKeyEfUiTestHost.CreateAsync();

        var getHtml = await host.Client.GetStringAsync("/efui/tenants/tenant%20north%3F1/edit");
        getHtml.Should().Contain("action=\"/efui/tenants/tenant%20north%3F1\"");
        getHtml.Should().Contain("name=\"Name\" value=\"North\"");
        getHtml.Should().NotContain("name=\"TenantKey\"");

        var response = await host.Client.PostAsync("/efui/tenants/tenant%20north%3F1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TenantKey"] = "tenant south?2",
            ["Name"] = "North Updated"
        }));

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.SeeOther);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/efui/tenants");

        var listHtml = await host.Client.GetStringAsync("/efui/tenants");
        listHtml.Should().Contain("North Updated");
        listHtml.Should().Contain("/efui/tenants/tenant%20north%3F1/edit");
        listHtml.Should().NotContain("/efui/tenants/tenant%20south%3F2/edit\">North Updated");
    }

    private sealed class StringKeyEfUiTestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly WebApplication _app;

        private StringKeyEfUiTestHost(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            _connection = connection;
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<StringKeyEfUiTestHost> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddDbContext<StringKeyDbContext>(options => options.UseSqlite(connection));

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<StringKeyDbContext>();
                await db.Database.EnsureCreatedAsync();
                db.Tenants.Add(new Tenant { TenantKey = "tenant north?1", Name = "North" });
                await db.SaveChangesAsync();
            }

            app.UseEfUi(options =>
            {
                options.DbContextType = typeof(StringKeyDbContext);
                options.RoutePrefix = "/efui";
                options.EnableInProduction = false;
            });

            await app.StartAsync();
            return new StringKeyEfUiTestHost(connection, app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class StringKeyDbContext(DbContextOptions<StringKeyDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>(builder =>
            {
                builder.ToTable("tenants");
                builder.HasKey(x => x.TenantKey);
                builder.Property(x => x.TenantKey).ValueGeneratedNever();
                builder.Property(x => x.Name).IsRequired();
            });
        }
    }

    private sealed class Tenant
    {
        public string TenantKey { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
