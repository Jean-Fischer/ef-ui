using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace EfUi.AspNetCore.Tests.Browser;

internal sealed class SampleHostProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _tempDirectory;
    private readonly StringBuilder _output;

    public Uri BaseUri { get; }

    private SampleHostProcess(Process process, string tempDirectory, Uri baseUri, StringBuilder output)
    {
        _process = process;
        _tempDirectory = tempDirectory;
        BaseUri = baseUri;
        _output = output;
    }

    public static async Task<SampleHostProcess> StartAsync()
    {
        var repoRoot = GetRepoRoot();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ef-ui-playwright", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        CopyFile(Path.Combine(repoRoot, "src", "EfUi.SampleHost", "sample.db"), Path.Combine(tempDirectory, "sample.db"));
        var chinookDbPath = Path.Combine(tempDirectory, "chinook.db");
        CopyFile(Path.Combine(repoRoot, "db", "chinook.db"), chinookDbPath);

        var port = GetFreeTcpPort();
        var baseUri = new Uri($"http://127.0.0.1:{port}");
        var hostDll = GetBuiltSampleHostPath(repoRoot);

        var output = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(hostDll);
        startInfo.Environment["ASPNETCORE_URLS"] = baseUri.ToString();
        startInfo.Environment["ASPNETCORE_CONTENTROOT"] = tempDirectory;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__Chinook"] = $"Data Source={chinookDbPath}";

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the sample host process.");

        process.OutputDataReceived += (_, eventArgs) => AppendOutput(output, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendOutput(output, eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var host = new SampleHostProcess(process, tempDirectory, baseUri, output);
        await host.WaitForStartupAsync().ConfigureAwait(false);
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
        }

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var chinookDb = Path.Combine(directory.FullName, "db", "chinook.db");
            var sampleDb = Path.Combine(directory.FullName, "src", "EfUi.SampleHost", "sample.db");

            if (File.Exists(chinookDb) && File.Exists(sampleDb))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root containing db/chinook.db and src/EfUi.SampleHost/sample.db.");
    }

    private static string GetBuiltSampleHostPath(string repoRoot)
    {
        var candidates = new[]
        {
            Path.Combine(repoRoot, "src", "EfUi.SampleHost", "bin", "Debug", "net8.0", "EfUi.SampleHost.dll"),
            Path.Combine(repoRoot, "src", "EfUi.SampleHost", "bin", "Release", "net8.0", "EfUi.SampleHost.dll")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new FileNotFoundException($"Expected built sample host in one of the following locations: {string.Join(", ", candidates)}", candidates[0]);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task WaitForStartupAsync()
    {
        using var client = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(2)
        };

        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        Exception? lastFailure = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"Sample host exited early with code {_process.ExitCode}.\n{GetOutput()}");
            }

            try
            {
                using var response = await client.GetAsync("/").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for the sample host to start.\nLast failure: {lastFailure?.Message}\n{GetOutput()}");
    }

    private string GetOutput()
    {
        lock (_output)
        {
            return _output.ToString();
        }
    }

    private static void AppendOutput(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }
}
