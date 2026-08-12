using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ui.Services;

public sealed class BackendProcessManager : IDisposable
{
    private const string BackendExecutableName = "Obsidian2Anki.Backend";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HealthRequestTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(150);

    private readonly ApiClient _apiClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly StringBuilder _diagnosticOutput = new();
    private Process? _process;
    private bool _startupCompleted;
    private bool _stopping;

    public BackendProcessManager(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public event Action<string>? UnexpectedExit;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("The local backend has already been started.");
        }

        var port = FindAvailablePort();
        var launch = ResolveBackendLaunch();
        var startInfo = new ProcessStartInfo
        {
            FileName = launch.ExecutablePath,
            WorkingDirectory = launch.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in launch.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString());
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        _process.OutputDataReceived += CaptureOutput;
        _process.ErrorDataReceived += CaptureOutput;
        _process.Exited += BackendProcess_Exited;

        try
        {
            if (!_process.Start())
            {
                throw new InvalidOperationException("The local backend process could not be started.");
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _apiClient.ConfigureBaseAddress(port);

            using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            startupCancellation.CancelAfter(StartupTimeout);

            await WaitUntilHealthyAsync(startupCancellation.Token);
            _startupCompleted = true;
        }
        catch (OperationCanceledException) when (
            !_lifetimeCancellation.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            StopProcess();
            throw new TimeoutException(
                $"The local backend did not become healthy within {StartupTimeout.TotalSeconds:0} seconds."
                + GetDiagnosticSuffix());
        }
        catch
        {
            StopProcess();
            throw;
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _lifetimeCancellation.Cancel();
        StopProcess();
        _lifetimeCancellation.Dispose();
    }

    private async Task WaitUntilHealthyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process is null || _process.HasExited)
            {
                var exitCode = _process?.ExitCode;
                throw new InvalidOperationException(
                    $"The local backend exited before it became ready"
                    + (exitCode is null ? "." : $" with code {exitCode}.")
                    + GetDiagnosticSuffix());
            }

            using var healthCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            healthCancellation.CancelAfter(HealthRequestTimeout);

            if (await _apiClient.IsHealthyAsync(healthCancellation.Token))
            {
                return;
            }

            await Task.Delay(HealthPollInterval, cancellationToken);
        }
    }

    private void BackendProcess_Exited(object? sender, EventArgs eventArgs)
    {
        if (_stopping || !_startupCompleted || _process is null)
        {
            return;
        }

        UnexpectedExit?.Invoke(
            $"The local backend exited unexpectedly with code {_process.ExitCode}."
            + GetDiagnosticSuffix());
    }

    private void CaptureOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            return;
        }

        lock (_diagnosticOutput)
        {
            _diagnosticOutput.AppendLine(eventArgs.Data);

            const int maximumCharacters = 4000;
            if (_diagnosticOutput.Length > maximumCharacters)
            {
                _diagnosticOutput.Remove(0, _diagnosticOutput.Length - maximumCharacters);
            }
        }
    }

    private string GetDiagnosticSuffix()
    {
        lock (_diagnosticOutput)
        {
            var output = _diagnosticOutput.ToString().Trim();
            return string.IsNullOrEmpty(output) ? string.Empty : $" Backend output: {output}";
        }
    }

    private void StopProcess()
    {
        var process = _process;
        _process = null;

        if (process is null)
        {
            return;
        }

        process.Exited -= BackendProcess_Exited;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and termination.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static BackendLaunch ResolveBackendLaunch()
    {
        var environmentPath = Environment.GetEnvironmentVariable("MD2ANKI_BACKEND_PATH");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var fullEnvironmentPath = Path.GetFullPath(environmentPath);
            if (!File.Exists(fullEnvironmentPath))
            {
                throw new FileNotFoundException(
                    "MD2ANKI_BACKEND_PATH does not point to an existing file.",
                    fullEnvironmentPath);
            }

            return new BackendLaunch(
                fullEnvironmentPath,
                Path.GetDirectoryName(fullEnvironmentPath)!,
                []);
        }

        var executableFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"{BackendExecutableName}.exe"
            : BackendExecutableName;
        var applicationDirectory = AppContext.BaseDirectory;
        var bundledCandidates = new[]
        {
            Path.Combine(applicationDirectory, "backend", executableFileName),
            Path.Combine(applicationDirectory, executableFileName),
        };

        foreach (var candidate in bundledCandidates)
        {
            if (File.Exists(candidate))
            {
                return new BackendLaunch(
                    candidate,
                    Path.GetDirectoryName(candidate)!,
                    []);
            }
        }

#if DEBUG
        var projectRoot = FindProjectRoot(applicationDirectory);
        if (projectRoot is not null)
        {
            var pythonPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(projectRoot, ".venv", "Scripts", "python.exe")
                : Path.Combine(projectRoot, ".venv", "bin", "python");
            var mainPath = Path.Combine(projectRoot, "main.py");

            if (File.Exists(pythonPath) && File.Exists(mainPath))
            {
                return new BackendLaunch(
                    pythonPath,
                    projectRoot,
                    [mainPath]);
            }
        }
#endif

        throw new FileNotFoundException(
            $"Could not find the bundled backend. Expected it at "
            + $"'{bundledCandidates[0]}'.");
    }

    private static string? FindProjectRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "main.py"))
                && Directory.Exists(Path.Combine(directory.FullName, "ui")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record BackendLaunch(
        string ExecutablePath,
        string WorkingDirectory,
        string[] PrefixArguments);
}
