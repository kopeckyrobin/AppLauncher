using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AppLauncher.Services;

public sealed class ProcessExitedEventArgs : EventArgs
{
    public required int ExitCode { get; init; }

    public required bool WasStopRequested { get; init; }
}

public sealed class ProcessRunner
{
    private readonly ConcurrentQueue<string> _pendingOutput = new();
    private Process? _process;
    private bool _stopRequested;

    public event EventHandler<ProcessExitedEventArgs>? Exited;

    public bool IsRunning
    {
        get
        {
            Process? process = this._process;
            return process is not null && !process.HasExited;
        }
    }

    public string BuildCommandLine(string projectFilePath, string profileName, string? targetFramework)
    {
        StringBuilder builder = new();
        builder.Append("dotnet run --project \"").Append(projectFilePath).Append('"');
        builder.Append(" --launch-profile \"").Append(profileName).Append('"');

        if (!String.IsNullOrEmpty(targetFramework))
        {
            builder.Append(" -f ").Append(targetFramework);
        }

        return builder.ToString();
    }

    public void Start(string projectFilePath, string profileName, string? targetFramework)
    {
        if (this.IsRunning)
        {
            return;
        }

        this._stopRequested = false;
        this._pendingOutput.Clear();

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(projectFilePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectFilePath);
        startInfo.ArgumentList.Add("--launch-profile");
        startInfo.ArgumentList.Add(profileName);

        if (!String.IsNullOrEmpty(targetFramework))
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(targetFramework);
        }

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += this.OnOutputDataReceived;
        process.ErrorDataReceived += this.OnOutputDataReceived;
        process.Exited += this.OnProcessExited;

        this._process = process;

        try
        {
            process.Start();
            ProcessJob.Assign(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception exception)
        {
            this._process = null;
            this._pendingOutput.Enqueue($"Nepodařilo se spustit proces: {exception.Message}");
            this.Exited?.Invoke(this, new ProcessExitedEventArgs { ExitCode = -1, WasStopRequested = false });
        }
    }

    public void Stop(int waitForExitMilliseconds = 0)
    {
        Process? process = this._process;
        if (process is null)
        {
            return;
        }

        this._stopRequested = true;

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

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is not null)
        {
            this._pendingOutput.Enqueue(eventArgs.Data);
        }
    }

    private void OnProcessExited(object? sender, EventArgs eventArgs)
    {
        Process? process = this._process;
        int exitCode = -1;

        if (process is not null)
        {
            try
            {
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            catch (Exception exception) when (exception is InvalidOperationException or SystemException)
            {
            }
        }

        this._process = null;

        this.Exited?.Invoke(this, new ProcessExitedEventArgs
        {
            ExitCode = exitCode,
            WasStopRequested = this._stopRequested
        });
    }
}
