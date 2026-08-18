using System.Text;
using System.Text.RegularExpressions;
using AppLauncher.Models;
using AppLauncher.Services;

namespace AppLauncher.ViewModels;

public sealed partial class ProjectViewModel : ObservableBase
{
    private const int MaximumLogLines = 600;
    private const int StartupGracePeriodSeconds = 10;

    [GeneratedRegex("(?:Now listening on|listening on):?\\s*(?<url>https?://[^\\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ListeningUrl();

    private readonly ScannedProject _project;
    private readonly AppStateStore _stateStore;
    private readonly Action<ProjectViewModel> _onSelected;
    private readonly Action _onRunStateChanged;
    private readonly ProcessRunner _runner = new();
    private readonly Queue<string> _logLines = new();
    private readonly RelayCommand _toggleCommand;
    private readonly RelayCommand _selectCommand;
    private readonly RelayCommand _clearLogCommand;
    private readonly RelayCommand _openUrlCommand;

    private readonly List<string> _detectedUrls = new();

    private LaunchProfile _selectedProfile;
    private RunState _state = RunState.Idle;
    private string _logText = String.Empty;
    private string _statusDetail = String.Empty;
    private bool _isSelected;
    private DateTime _startedAt;

    public ProjectViewModel(
        ScannedProject project,
        string solutionName,
        AppStateStore stateStore,
        Action<ProjectViewModel> onSelected,
        Action onRunStateChanged)
    {
        this._project = project;
        this._stateStore = stateStore;
        this._onSelected = onSelected;
        this._onRunStateChanged = onRunStateChanged;
        this.SolutionName = solutionName;

        string? remembered = stateStore.GetLastProfile(project.ProjectFilePath);
        this._selectedProfile = project.Profiles.FirstOrDefault(
            profile => String.Equals(profile.Name, remembered, StringComparison.Ordinal)) ?? project.Profiles[0];

        this._toggleCommand = new RelayCommand(this.Toggle);
        this._selectCommand = new RelayCommand(this.Select);
        this._clearLogCommand = new RelayCommand(this.ClearLog);
        this._openUrlCommand = new RelayCommand(this.OpenUrl);

        this._runner.Exited += this.OnRunnerExited;
    }

    public string Name
    {
        get { return this._project.Name; }
    }

    public string SolutionName { get; }

    public string ProjectFilePath
    {
        get { return this._project.ProjectFilePath; }
    }

    public IReadOnlyList<LaunchProfile> Profiles
    {
        get { return this._project.Profiles; }
    }

    public bool HasMultipleProfiles
    {
        get { return this._project.Profiles.Count > 1; }
    }

    public bool UsesUserSecrets
    {
        get { return this._project.UsesUserSecrets; }
    }

    public string CommandLine
    {
        get { return this._runner.BuildCommandLine(this._project.ProjectFilePath, this._selectedProfile.Name, this._project.TargetFrameworkOverride); }
    }

    public System.Windows.Input.ICommand ToggleCommand
    {
        get { return this._toggleCommand; }
    }

    public System.Windows.Input.ICommand SelectCommand
    {
        get { return this._selectCommand; }
    }

    public System.Windows.Input.ICommand ClearLogCommand
    {
        get { return this._clearLogCommand; }
    }

    public System.Windows.Input.ICommand OpenUrlCommand
    {
        get { return this._openUrlCommand; }
    }

    public LaunchProfile SelectedProfile
    {
        get { return this._selectedProfile; }
        set
        {
            if (value is null)
            {
                return;
            }

            if (this.SetProperty(ref this._selectedProfile, value))
            {
                this._stateStore.SetLastProfile(this._project.ProjectFilePath, value.Name);
                this.RaisePropertyChanged(nameof(this.EnvironmentBadge));
                this.RaisePropertyChanged(nameof(this.CommandLine));
                this.RaiseEndpointChanged();
            }
        }
    }

    public string EnvironmentBadge
    {
        get
        {
            if (String.IsNullOrEmpty(this._selectedProfile.EnvironmentName))
            {
                return String.Empty;
            }

            return this._selectedProfile.EnvironmentName;
        }
    }

    public RunState State
    {
        get { return this._state; }
        private set
        {
            if (this.SetProperty(ref this._state, value))
            {
                this.RaisePropertyChanged(nameof(this.IsRunning));
                this.RaisePropertyChanged(nameof(this.IsBusy));
                this.RaisePropertyChanged(nameof(this.ActionLabel));
                this.RaisePropertyChanged(nameof(this.StatusText));
                this.RaisePropertyChanged(nameof(this.HasStatusText));
                this.RaisePropertyChanged(nameof(this.CanChangeProfile));
                this._onRunStateChanged();
            }
        }
    }

    public bool IsRunning
    {
        get { return this._state is RunState.Running or RunState.Starting or RunState.Stopping; }
    }

    public bool IsBusy
    {
        get { return this._state is RunState.Starting or RunState.Stopping; }
    }

    public bool CanChangeProfile
    {
        get { return !this.IsRunning; }
    }

    public string ActionLabel
    {
        get
        {
            if (this.IsRunning)
            {
                return "Stop";
            }

            return "Run";
        }
    }

    public string StatusText
    {
        get
        {
            switch (this._state)
            {
                case RunState.Starting:
                    return "spouští se";
                case RunState.Running:
                    return "běží";
                case RunState.Stopping:
                    return "ukončuje se";
                case RunState.Exited:
                    return this._statusDetail;
                case RunState.Failed:
                    return this._statusDetail;
                default:
                    return String.Empty;
            }
        }
    }

    public bool HasStatusText
    {
        get { return !String.IsNullOrEmpty(this.StatusText); }
    }

    public bool IsSelected
    {
        get { return this._isSelected; }
        set { this.SetProperty(ref this._isSelected, value); }
    }

    public string LogText
    {
        get { return this._logText; }
        private set { this.SetProperty(ref this._logText, value); }
    }

    public bool HasLog
    {
        get { return this._logLines.Count > 0; }
    }

    public string DisplayUrl
    {
        get
        {
            foreach (string url in this._detectedUrls)
            {
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }
            }

            if (this._detectedUrls.Count > 0)
            {
                return this._detectedUrls[0];
            }

            return this._selectedProfile.PrimaryUrl;
        }
    }

    public bool HasDisplayUrl
    {
        get { return !String.IsNullOrEmpty(this.DisplayUrl); }
    }

    public string PortLabel
    {
        get
        {
            List<string> ports = new();

            foreach (string url in this._detectedUrls)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
                {
                    continue;
                }

                string port = parsed.Port.ToString();
                if (!ports.Contains(port))
                {
                    ports.Add(port);
                }
            }

            if (ports.Count == 0)
            {
                return String.Empty;
            }

            return ":" + String.Join("  :", ports);
        }
    }

    public bool HasPorts
    {
        get { return !String.IsNullOrEmpty(this.PortLabel); }
    }

    public void Toggle()
    {
        if (this.IsRunning)
        {
            this.Stop();
        }
        else
        {
            this.Start();
        }
    }

    public void Start()
    {
        if (this.IsRunning)
        {
            return;
        }

        this._stateStore.SetLastProfile(this._project.ProjectFilePath, this._selectedProfile.Name);

        this._logLines.Clear();
        this._detectedUrls.Clear();
        this._statusDetail = String.Empty;
        this.LogText = String.Empty;
        this._startedAt = DateTime.UtcNow;

        this.AppendLine($"> {this.CommandLine}");
        this.AppendLine(String.Empty);
        this.FlushLog();

        this.State = RunState.Starting;
        this.RaiseEndpointChanged();

        this._runner.Start(this._project.ProjectFilePath, this._selectedProfile.Name, this._project.TargetFrameworkOverride);
        this._onSelected(this);
    }

    public void Stop(int waitForExitMilliseconds = 0)
    {
        if (this._state == RunState.Idle || this._state == RunState.Exited || this._state == RunState.Failed)
        {
            return;
        }

        this.State = RunState.Stopping;
        this._runner.Stop(waitForExitMilliseconds);
    }

    public void Pump()
    {
        bool changed = false;

        while (this._runner.TryDequeueOutput(out string line))
        {
            this.AppendLine(line);
            this.InspectLine(line);
            changed = true;
        }

        if (changed)
        {
            this.FlushLog();
        }

        if (this._state == RunState.Starting &&
            this._runner.IsRunning &&
            (DateTime.UtcNow - this._startedAt).TotalSeconds > StartupGracePeriodSeconds)
        {
            this.State = RunState.Running;
        }
    }

    private void InspectLine(string line)
    {
        Match match = ListeningUrl().Match(line);
        if (!match.Success)
        {
            return;
        }

        string url = match.Groups["url"].Value.TrimEnd('.', ',', ';');

        if (!this._detectedUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
        {
            this._detectedUrls.Add(url);
            this.RaiseEndpointChanged();
        }

        if (this._state == RunState.Starting)
        {
            this.State = RunState.Running;
        }
    }

    private void RaiseEndpointChanged()
    {
        this.RaisePropertyChanged(nameof(this.DisplayUrl));
        this.RaisePropertyChanged(nameof(this.HasDisplayUrl));
        this.RaisePropertyChanged(nameof(this.PortLabel));
        this.RaisePropertyChanged(nameof(this.HasPorts));
    }

    private void AppendLine(string line)
    {
        this._logLines.Enqueue(line);

        while (this._logLines.Count > MaximumLogLines)
        {
            this._logLines.Dequeue();
        }
    }

    private void FlushLog()
    {
        StringBuilder builder = new();

        foreach (string line in this._logLines)
        {
            builder.AppendLine(line);
        }

        this.LogText = builder.ToString();
        this.RaisePropertyChanged(nameof(this.HasLog));
    }

    private void Select()
    {
        this._onSelected(this);
    }

    private void ClearLog()
    {
        this._logLines.Clear();
        this.LogText = String.Empty;
        this.RaisePropertyChanged(nameof(this.HasLog));
    }

    private void OpenUrl()
    {
        string url = this.DisplayUrl;
        if (String.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (UriFormatException)
        {
        }
    }

    private void OnRunnerExited(object? sender, ProcessExitedEventArgs eventArgs)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            this.Pump();

            if (eventArgs.WasStopRequested)
            {
                this._statusDetail = "zastaveno";
                this.State = RunState.Exited;
            }
            else if (eventArgs.ExitCode == 0)
            {
                this._statusDetail = "dokončeno";
                this.State = RunState.Exited;
            }
            else
            {
                this._statusDetail = $"chyba ({eventArgs.ExitCode})";
                this.State = RunState.Failed;
            }

            this._detectedUrls.Clear();
            this.RaiseEndpointChanged();

            this.AppendLine(String.Empty);
            this.AppendLine($"— proces ukončen, kód {eventArgs.ExitCode} —");
            this.FlushLog();
        });
    }
}
