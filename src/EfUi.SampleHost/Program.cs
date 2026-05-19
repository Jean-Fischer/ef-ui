using EfUi.AspNetCore;
using EfUi.SampleHost.Chinook;
using EfUi.SampleHost.Data;
using EfUi.SampleHost.EdgeCases;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/", () => Results.Content(
    "<html><body><h1>EF UI Samples</h1><p>Choose a database.</p><ul><li><a href=\"/simple\">Simple</a></li><li><a href=\"/edge-cases\">Edge cases</a></li><li><a href=\"/chinook\">Chinook</a></li></ul></body></html>",
    "text/html"));

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
});

app.Run();

return;

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
