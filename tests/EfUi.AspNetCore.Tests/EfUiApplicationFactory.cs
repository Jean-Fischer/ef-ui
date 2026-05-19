using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EfUi.AspNetCore.Tests;

public sealed class EfUiApplicationFactory : AppHostFactoryBase
{
    protected override string EnvironmentName => "Development";
}

public sealed class ProductionEfUiApplicationFactory : AppHostFactoryBase
{
    protected override string EnvironmentName => "Production";
}

public abstract class AppHostFactoryBase : WebApplicationFactory<Program>
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "ef-ui-tests", Guid.NewGuid().ToString("N"));
    private string TempSampleDbPath => Path.Combine(_tempDirectory, "sample.db");
    private string TempEdgeCasesDbPath => Path.Combine(_tempDirectory, "edge-cases.db");
    private string TempChinookDbPath => Path.Combine(_tempDirectory, "chinook.db");

    protected abstract string EnvironmentName { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDirectory);
        File.Copy(GetSourceChinookDbPath(), TempChinookDbPath, overwrite: true);

        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sample"] = $"Data Source={TempSampleDbPath}",
                ["ConnectionStrings:EdgeCases"] = $"Data Source={TempEdgeCasesDbPath}",
                ["ConnectionStrings:Chinook"] = $"Data Source={TempChinookDbPath}"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || !Directory.Exists(_tempDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetSourceChinookDbPath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/chinook.db"));
}
