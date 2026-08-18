using AppLauncher.Services;

namespace AppLauncher.ViewModels;

public enum UpdateStage
{
    Hidden,
    Available,
    Downloading,
    ReadyToRestart,
    Failed
}

public sealed class UpdateViewModel : ObservableBase
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    private readonly UpdateService _service = new();
    private readonly RelayCommand _actionCommand;

    private IDispatcherTimer? _checkTimer;
    private UpdateStage _stage = UpdateStage.Hidden;
    private string _availableVersion = String.Empty;
    private int _progress;
    private bool _isChecking;

    public UpdateViewModel()
    {
        this._actionCommand = new RelayCommand(this.ExecuteAction, this.CanExecuteAction);
    }

    public Func<Task<bool>>? RestartGuard { get; set; }

    public System.Windows.Input.ICommand ActionCommand
    {
        get { return this._actionCommand; }
    }

    public string CurrentVersion
    {
        get { return this._service.CurrentVersion; }
    }

    public bool IsVisible
    {
        get { return this._stage != UpdateStage.Hidden; }
    }

    public bool HasAction
    {
        get { return this._stage is UpdateStage.Available or UpdateStage.ReadyToRestart or UpdateStage.Failed; }
    }

    public string StatusText
    {
        get
        {
            switch (this._stage)
            {
                case UpdateStage.Available:
                    return $"Nová verze {this._availableVersion}";

                case UpdateStage.Downloading:
                    return $"Stahuji {this._progress} %";

                case UpdateStage.ReadyToRestart:
                    return $"Verze {this._availableVersion} připravena";

                case UpdateStage.Failed:
                    return "Aktualizace selhala";

                default:
                    return String.Empty;
            }
        }
    }

    public string ActionText
    {
        get
        {
            switch (this._stage)
            {
                case UpdateStage.Available:
                    return "Aktualizovat";

                case UpdateStage.ReadyToRestart:
                    return "Restartovat";

                case UpdateStage.Failed:
                    return "Zkusit znovu";

                default:
                    return String.Empty;
            }
        }
    }

    public void Initialize()
    {
        if (!this._service.IsEnabled)
        {
            return;
        }

        this._checkTimer = Application.Current?.Dispatcher.CreateTimer();

        if (this._checkTimer is not null)
        {
            this._checkTimer.Interval = CheckInterval;
            this._checkTimer.Tick += this.OnCheckTick;
            this._checkTimer.Start();
        }

        _ = this.CheckAsync();
    }

    public void Shutdown()
    {
        this._checkTimer?.Stop();
    }

    private void OnCheckTick(object? sender, EventArgs eventArgs)
    {
        _ = this.CheckAsync();
    }

    private async Task CheckAsync()
    {
        if (this._isChecking)
        {
            return;
        }

        if (this._stage is UpdateStage.Downloading or UpdateStage.ReadyToRestart)
        {
            return;
        }

        this._isChecking = true;

        try
        {
            bool hasUpdate = await this._service.CheckAsync();

            if (hasUpdate)
            {
                this._availableVersion = this._service.PendingVersion;
                this.SetStage(UpdateStage.Available);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            this._isChecking = false;
        }
    }

    private bool CanExecuteAction()
    {
        return this.HasAction;
    }

    private void ExecuteAction()
    {
        if (this._stage == UpdateStage.ReadyToRestart)
        {
            _ = this.RestartAsync();
            return;
        }

        _ = this.DownloadAsync();
    }

    private async Task DownloadAsync()
    {
        this.Progress = 0;
        this.SetStage(UpdateStage.Downloading);

        try
        {
            await this._service.DownloadAsync(this.ReportProgress, CancellationToken.None);
            this.SetStage(UpdateStage.ReadyToRestart);
        }
        catch (Exception)
        {
            this.SetStage(UpdateStage.Failed);
        }
    }

    private async Task RestartAsync()
    {
        if (this.RestartGuard is not null)
        {
            bool canRestart = await this.RestartGuard();

            if (!canRestart)
            {
                return;
            }
        }

        this._checkTimer?.Stop();

        try
        {
            this._service.ApplyAndRestart();
        }
        catch (Exception)
        {
            this.SetStage(UpdateStage.Failed);
        }
    }

    private void ReportProgress(int value)
    {
        MainThread.BeginInvokeOnMainThread(() => this.Progress = value);
    }

    private int Progress
    {
        get { return this._progress; }
        set
        {
            if (this.SetProperty(ref this._progress, value))
            {
                this.RaisePropertyChanged(nameof(this.StatusText));
            }
        }
    }

    private void SetStage(UpdateStage stage)
    {
        if (this._stage == stage)
        {
            return;
        }

        this._stage = stage;

        this.RaisePropertyChanged(nameof(this.IsVisible));
        this.RaisePropertyChanged(nameof(this.HasAction));
        this.RaisePropertyChanged(nameof(this.StatusText));
        this.RaisePropertyChanged(nameof(this.ActionText));
        this._actionCommand.RaiseCanExecuteChanged();
    }
}
