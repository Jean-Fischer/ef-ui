using System.Net;
using System.Security.Claims;
using EfUi.AspNetCore;
using EfUi.SampleHost.Chinook;
using EfUi.SampleHost.Data;
using EfUi.SampleHost.EdgeCases;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".EfUi.SampleHost.Auth";
        options.SlidingExpiration = false;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context => RespondWithStatusCode(context, StatusCodes.Status401Unauthorized),
            OnRedirectToAccessDenied = context => RespondWithStatusCode(context, StatusCodes.Status403Forbidden)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite(CreateSqliteConnectionString(
        builder.Environment.ContentRootPath,
        builder.Configuration.GetConnectionString("Sample"),
        "sample.db")));

builder.Services.AddDbContext<ChinookDbContext>(options =>
    options.UseSqlite(CreateSqliteConnectionString(
        builder.Environment.ContentRootPath,
        builder.Configuration.GetConnectionString("Chinook"),
        Path.Combine("..", "..", "db", "chinook.db"))));

builder.Services.AddDbContext<EdgeCaseDbContext>(options =>
    options.UseSqlite(CreateSqliteConnectionString(
        builder.Environment.ContentRootPath,
        builder.Configuration.GetConnectionString("EdgeCases"),
        "edge-cases.db")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sampleDb = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await SampleDbSeeder.SeedAsync(sampleDb);

    var edgeCaseDb = scope.ServiceProvider.GetRequiredService<EdgeCaseDbContext>();
    await EdgeCaseDbSeeder.SeedAsync(edgeCaseDb);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext httpContext) => Results.Content(RenderHomePage(httpContext.User), "text/html"));
app.MapPost("/auth/anonymous", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});
app.MapPost("/auth/readonly", async (HttpContext httpContext) =>
{
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal("ReadOnly"));
    return Results.Redirect("/");
});
app.MapPost("/auth/edit", async (HttpContext httpContext) =>
{
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal("Edit"));
    return Results.Redirect("/");
});

app.UseEfUi(options =>
{
    options.DbContextType = typeof(SampleDbContext);
    options.RoutePrefix = "/simple";
    options.EnableInProduction = true;
});

app.UseEfUi(options =>
{
    options.DbContextType = typeof(EdgeCaseDbContext);
    options.RoutePrefix = "/edge-cases";
    options.EnableInProduction = true;
});

app.UseEfUi(options =>
{
    options.DbContextType = typeof(ChinookDbContext);
    options.RoutePrefix = "/chinook";
    options.EnableInProduction = true;
    options.RequireAuthorization = true;
});

app.Run();

return;

static ClaimsPrincipal CreatePrincipal(string role)
{
    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim(ClaimTypes.Name, role));
    identity.AddClaim(new Claim(ClaimTypes.Role, role));
    return new ClaimsPrincipal(identity);
}

static Task RespondWithStatusCode(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    context.Response.StatusCode = statusCode;
    return Task.CompletedTask;
}

static string RenderHomePage(ClaimsPrincipal user)
{
    var profile = user.Identity?.IsAuthenticated == true
        ? user.FindFirstValue(ClaimTypes.Role) ?? user.Identity?.Name ?? "Authenticated"
        : "Anonymous";

    return $"""
        <html>
        <body>
            <h1>EF UI Samples</h1>
            <p>Choose a database.</p>
            <p><strong>Current profile:</strong> {WebUtility.HtmlEncode(profile)}</p>
            <form method="post" action="/auth/anonymous" style="display:inline-block; margin-right: 0.5rem;">
                <button type="submit">Anonymous</button>
            </form>
            <form method="post" action="/auth/readonly" style="display:inline-block; margin-right: 0.5rem;">
                <button type="submit">ReadOnly</button>
            </form>
            <form method="post" action="/auth/edit" style="display:inline-block; margin-right: 1rem;">
                <button type="submit">Edit</button>
            </form>
            <ul>
                <li><a href="/simple">Simple</a></li>
                <li><a href="/edge-cases">Edge Cases</a></li>
                <li><a href="/chinook">Chinook</a></li>
            </ul>
        </body>
        </html>
        """;
}

static string CreateSqliteConnectionString(string contentRootPath, string? configuredConnectionString, string fallbackDataSource)
{
    var builder = new SqliteConnectionStringBuilder(configuredConnectionString ?? $"Data Source={fallbackDataSource}");
    var dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? fallbackDataSource : builder.DataSource;

    if (!Path.IsPathRooted(dataSource))
    {
        dataSource = Path.GetFullPath(Path.Combine(contentRootPath, dataSource));
    }

    builder.DataSource = dataSource;
    return builder.ToString();
}

public partial class Program;
