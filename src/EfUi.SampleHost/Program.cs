using EfUi.SampleHost.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Sample") ?? "Data Source=sample.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await SampleDbSeeder.SeedAsync(db);
}

app.MapGet("/", () => Results.Redirect("/efui"));

app.Run();

public partial class Program;
