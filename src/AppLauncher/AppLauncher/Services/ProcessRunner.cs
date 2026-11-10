using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AppLauncher.Models;

namespace AppLauncher.Services;

public sealed class ProcessExitedEventArgs : EventArgs
{
    public required int ExitCode { get; init; }

    public required bool WasStopRequested { get; init; }
}

public sealed class ProcessRunner
{
    private const string DisableNodeReuseVariable = "MSBUILDDISABLENODEREUSE";

    private readonly ConcurrentQueue<string> _pendingOutput = new();
    private volatile Process? _process;
    private volatile bool _stopRequested;
    private volatile bool _isActive;

    public event EventHandler<ProcessExitedEventArgs>? Exited;

    public bool IsRunning
    {
        get { return this._isActive; }
    }

    public string BuildCommandLine(string projectFilePath, string? targetFramework)
    {
        StringBuilder builder = new();
        builder.Append("dotnet build \"").Append(projectFilePath).Append('"');

        if (!String.IsNullOrEmpty(targetFramework))
        {
            builder.Append(" -f ").Append(targetFramework);
        }

        builder.Append(" && ").Append(Path.GetFileNameWithoutExtension(projectFilePath)).Append(".exe");
        return builder.ToString();
    }

    public void Start(string projectFilePath, LaunchProfile profile, string? targetFramework)
    {
        if (this.IsRunning)
        {
            return;
        }

        this._stopRequested = false;
        this._isActive = true;
        this._pendingOutput.Clear();

        _ = Task.Run(() => this.RunAsync(projectFilePath, profile, targetFramework));
    }

    public void Stop(int waitForExitMilliseconds = 0)
    {
        this._stopRequested = true;

        Process? process = this._process;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            if (waitForExitMilliseconds > 0)
            {
                process.WaitForExit(waitForExitMilliseconds);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    public bool TryDequeueOutput(out string line)
    {
        return this._pendingOutput.TryDequeue(out line!);
    }

    private async Task RunAsync(string projectFilePath, LaunchProfile profile, string? targetFramework)
    {
        int exitCode = -1;

        try
        {
            exitCode = await this.RunPipelineAsync(projectFilePath, profile, targetFramework);
        }
        catch (Exception exception)
        {
            this._pendingOutput.Enqueue($"Nepodařilo se spustit proces: {exception.Message}");
        }
        finally
        {
            this._process = null;
            this._isActive = false;
        }

        this.Exited?.Invoke(this, new ProcessExitedEventArgs
        {
            ExitCode = exitCode,
            WasStopRequested = this._stopRequested
        });
    }

    private async Task<int> RunPipelineAsync(string projectFilePath, LaunchProfile profile, string? targetFramework)
    {
        string projectDirectory = Path.GetDirectoryName(projectFilePath) ?? Environment.CurrentDirectory;
        string frameworkArguments = String.IsNullOrEmpty(targetFramework) ? String.Empty : " -f " + targetFramework;

        string buildArguments = $"build \"{projectFilePath}\" --nologo -nodeReuse:false{frameworkArguments}";
        this._pendingOutput.Enqueue("> dotnet " + buildArguments);
        this._pendingOutput.Enqueue(String.Empty);

        int buildExitCode = await this.RunProcessAsync("dotnet", buildArguments, projectDirectory, null, true);
        if (this._stopRequested || buildExitCode != 0)
        {
            return buildExitCode;
        }

        string? targetPath = await this.ResolveTargetPathAsync(projectFilePath, targetFramework, projectDirectory);
        string? executablePath = FindExecutable(targetPath);

        Dictionary<string, string> environment = BuildEnvironment(profile);
        string workingDirectory = ResolveWorkingDirectory(profile, projectDirectory);
        string profileArguments = profile.CommandLineArguments ?? String.Empty;

        this._pendingOutput.Enqueue(String.Empty);

        if (executablePath is not null)
        {
            this._pendingOutput.Enqueue(("> " + executablePath + " " + profileArguments).TrimEnd());
            this._pendingOutput.Enqueue(String.Empty);
            return await this.RunProcessAsync(executablePath, profileArguments, workingDirectory, environment, true);
        }

        if (!String.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
        {
            string hostArguments = ("\"" + targetPath + "\" " + profileArguments).TrimEnd();
            this._pendingOutput.Enqueue("> dotnet " + hostArguments);
            this._pendingOutput.Enqueue(String.Empty);
            return await this.RunProcessAsync("dotnet", hostArguments, workingDirectory, environment, true);
        }

        string runArguments = $"run --no-build --project \"{projectFilePath}\" --launch-profile \"{profile.Name}\"{frameworkArguments}";
        this._pendingOutput.Enqueue("> dotnet " + runArguments);
        this._pendingOutput.Enqueue(String.Empty);
        return await this.RunProcessAsync("dotnet", runArguments, projectDirectory, null, true);
    }

    private async Task<string?> ResolveTargetPathAsync(string projectFilePath, string? targetFramework, string projectDirectory)
    {
        string arguments = $"msbuild \"{projectFilePath}\" -getProperty:TargetPath -nodeReuse:false";
        if (!String.IsNullOrEmpty(targetFramework))
        {
            arguments += " -p:TargetFramework=" + targetFramework;
        }

        StringBuilder output = new();
        int exitCode = await this.RunProcessAsync("dotnet", arguments, projectDirectory, null, false, output);
        if (exitCode != 0)
        {
            return null;
        }

        string path = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? String.Empty;

        return String.IsNullOrEmpty(path) ? null : path;
    }

    private static string? FindExecutable(string? targetPath)
    {
        if (String.IsNullOrEmpty(targetPath))
        {
            return null;
        }

        if (targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(targetPath) ? targetPath : null;
        }

        string candidate = Path.ChangeExtension(targetPath, ".exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private static Dictionary<string, string> BuildEnvironment(LaunchProfile profile)
    {
        Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_LAUNCH_PROFILE"] = profile.Name
        };

        if (!String.IsNullOrEmpty(profile.ApplicationUrl))
        {
            environment["ASPNETCORE_URLS"] = profile.ApplicationUrl;
        }

        foreach (KeyValuePair<string, string> variable in profile.EnvironmentVariables)
        {
            environment[variable.Key] = Environment.ExpandEnvironmentVariables(variable.Value);
        }

        return environment;
    }

    private static string ResolveWorkingDirectory(LaunchProfile profile, string projectDirectory)
    {
        if (String.IsNullOrEmpty(profile.WorkingDirectory))
        {
            return projectDirectory;
        }

        return Path.GetFullPath(profile.WorkingDirectory, projectDirectory);
    }

    private async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        bool writeToLog,
        StringBuilder? capture = null)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.Environment[DisableNodeReuseVariable] = "1";

        if (environment is not null)
        {
            foreach (KeyValuePair<string, string> variable in environment)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        using Process process = new() { StartInfo = startInfo };

        process.OutputDataReceived += (_, eventArgs) => this.OnOutputLine(eventArgs.Data, writeToLog, capture);
        process.ErrorDataReceived += (_, eventArgs) => this.OnOutputLine(eventArgs.Data, writeToLog, null);

        process.Start();
        this._process = process;
        ProcessJob.Assign(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (this._stopRequested)
        {
            this.Stop();
        }

        await process.WaitForExitAsync();
        process.WaitForExit();

        this._process = null;
        return process.ExitCode;
    }

    private void OnOutputLine(string? line, bool writeToLog, StringBuilder? capture)
    {
        if (line is null)
        {
            return;
        }

        if (writeToLog)
        {
            this._pendingOutput.Enqueue(line);
        }
        else if (capture is not null)
        {
            lock (capture)
            {
                capture.AppendLine(line);
            }
        }
    }
}
