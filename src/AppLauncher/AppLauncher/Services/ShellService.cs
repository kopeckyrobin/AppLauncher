using System.Diagnostics;

namespace AppLauncher.Services;

public static class ShellService
{
    public static void OpenFolder(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        Start(new ProcessStartInfo
        {
            FileName = directoryPath,
            UseShellExecute = true
        });
    }

    public static void OpenTerminal(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        bool started = Start(new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            WorkingDirectory = directoryPath,
            UseShellExecute = true
        });

        if (started)
        {
            return;
        }

        Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = directoryPath,
            UseShellExecute = true
        });
    }

    private static bool Start(ProcessStartInfo startInfo)
    {
        try
        {
            Process? process = Process.Start(startInfo);
            process?.Dispose();
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }
}
