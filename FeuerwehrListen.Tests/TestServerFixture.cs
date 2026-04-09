using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FeuerwehrListen.Tests;

/// <summary>
/// Starts the real app as a child process with a fresh temp SQLite DB on a random free port.
/// </summary>
public class TestServerFixture : IDisposable
{
    private Process? _process;
    public string BaseUrl { get; private set; } = "";
    public string DbPath { get; private set; } = "";

    public void Start()
    {
        var port = GetFreePort();
        DbPath = Path.Combine(Path.GetTempPath(), $"feuerwehr_test_{Guid.NewGuid():N}.db");
        BaseUrl = $"http://localhost:{port}";

        var appDll = Path.Combine(AppContext.BaseDirectory, "FeuerwehrListen.dll");
        if (!File.Exists(appDll))
            throw new FileNotFoundException($"App DLL not found at {appDll}");

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{appDll}\"",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        _process.StartInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        _process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        _process.StartInfo.Environment["DATABASE_CONNECTION_STRING"] = $"Data Source={DbPath}";

        _process.Start();

        // Drain stdout/stderr asynchronously to prevent buffer deadlock
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var ready = WaitForServer(BaseUrl, TimeSpan.FromSeconds(30));
        if (!ready)
        {
            throw new Exception($"Server did not start within 30s on {BaseUrl}");
        }
    }

    private static bool WaitForServer(string url, TimeSpan timeout)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = client.GetAsync(url).Result;
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                    return true;
            }
            catch { }
            Thread.Sleep(500);
        }
        return false;
    }

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            catch { }
        }
        _process?.Dispose();

        if (File.Exists(DbPath))
        {
            try { File.Delete(DbPath); } catch { }
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
