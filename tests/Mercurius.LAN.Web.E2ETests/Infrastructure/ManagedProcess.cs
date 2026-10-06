using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Mercurius.LAN.Web.E2ETests.Infrastructure;

internal sealed class ManagedProcess : IAsyncDisposable
{
    private static readonly Regex ListeningAddress = new("Now listening on:\\s*(https?://\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Process _process;
    private readonly TaskCompletionSource<Uri> _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<string> _output = new();
    private readonly object _logLock = new();
    private readonly StreamWriter _logWriter;
    private bool _logClosed;

    private ManagedProcess(Process process, string outputLogPath)
    {
        _process = process;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputLogPath))!);
        _logWriter = new StreamWriter(new FileStream(
            outputLogPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite), Encoding.UTF8)
        {
            AutoFlush = true
        };

        try
        {
            _process.OutputDataReceived += (_, args) => ObserveLine("stdout", args.Data);
            _process.ErrorDataReceived += (_, args) => ObserveLine("stderr", args.Data);
            _process.Exited += (_, _) => _listening.TrySetException(new InvalidOperationException($"Process exited with code {_process.ExitCode}. {RecentOutput}"));
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch
        {
            lock (_logLock)
            {
                _logClosed = true;
                _logWriter.Dispose();
            }
            throw;
        }
    }

    public Uri BaseUri => _listening.Task.GetAwaiter().GetResult();

    public string RecentOutput => string.Join(Environment.NewLine, _output.ToArray());

    public static async Task<ManagedProcess> StartWebProjectAsync(
        string projectPath,
        IReadOnlyDictionary<string, string> environment,
        string outputLogPath,
        CancellationToken cancellationToken = default)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"The project path has no parent directory: {projectPath}");
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configuration = outputDirectory.Parent?.Name ?? "Debug";
        var targetFramework = outputDirectory.Name;
        var project = XDocument.Load(projectPath);
        var assemblyName = project.Descendants().FirstOrDefault(element => element.Name.LocalName == "AssemblyName")?.Value
            ?? Path.GetFileNameWithoutExtension(projectPath);
        var assemblyPath = Path.Combine(projectDirectory, "bin", configuration, targetFramework, assemblyName + ".dll");
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Build {Path.GetFileName(projectPath)} in {configuration} before starting the E2E application.", assemblyPath);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add("http://127.0.0.1:0");
        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(projectPath)}.");
        ManagedProcess managed;
        try
        {
            managed = new ManagedProcess(process, outputLogPath);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // The child may already have exited while its output readers were being attached.
            }
            finally
            {
                process.Dispose();
            }
            throw;
        }

        process.EnableRaisingEvents = true;
        if (process.HasExited)
            managed._listening.TrySetException(new InvalidOperationException($"Process exited with code {process.ExitCode}. {managed.RecentOutput}"));
        try
        {
            await managed._listening.Task.WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);
            return managed;
        }
        catch (Exception exception)
        {
            await managed.DisposeAsync();
            throw new InvalidOperationException($"{Path.GetFileName(projectPath)} did not start. {managed.RecentOutput}", exception);
        }
    }

    private void ObserveLine(string source, string? line)
    {
        if (line is null)
            return;

        lock (_logLock)
        {
            if (!_logClosed)
                _logWriter.WriteLine($"{DateTimeOffset.UtcNow:O} [{source}] {line}");
        }

        if (!string.IsNullOrWhiteSpace(line))
        {
            _output.Enqueue($"[{source}] {line}");
            while (_output.Count > 200)
                _output.TryDequeue(out _);
        }

        var match = ListeningAddress.Match(line);
        if (match.Success && Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out var uri))
            _listening.TrySetResult(uri);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
            // WaitForExit() also waits for the redirected output/error event handlers to drain.
            _process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // Process.Start can fail before the child is associated with its Process wrapper.
        }
        finally
        {
            try
            {
                lock (_logLock)
                {
                    _logClosed = true;
                    _logWriter.Dispose();
                }
            }
            finally
            {
                _process.Dispose();
            }
        }
    }
}
