using System.Collections.ObjectModel;
using AppLauncher.Models;
using AppLauncher.Services;

namespace AppLauncher.ViewModels;

public sealed class MainViewModel : ObservableBase
{
    private readonly AppStateStore _stateStore = new();
    private readonly RepositoryScanner _scanner = new();
    private readonly List<ProjectViewModel> _allProjects = new();
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _stopAllCommand;
    private readonly RelayCommand _changeRootCommand;
    private readonly RelayCommand _openRootCommand;

    private IDispatcherTimer? _pumpTimer;
    private CancellationTokenSource? _scanCancellation;
    private ProjectViewModel? _selectedProject;
    private string _rootPath = String.Empty;
    private bool _isScanning;
    private string _emptyMessage = String.Empty;

    public MainViewModel()
    {
        this._stateStore.Load();
        this._rootPath = this._stateStore.RepositoriesRoot;

        this._refreshCommand = new RelayCommand(this.Refresh);
        this._stopAllCommand = new RelayCommand(this.StopAll);
        this._changeRootCommand = new RelayCommand(this.ChangeRoot);
        this._openRootCommand = new RelayCommand(this.OpenRoot);
    }

    public ObservableCollection<RepositoryViewModel> Repositories { get; } = new();

    public GitDiffViewModel GitDiff { get; } = new();

    public System.Windows.Input.ICommand RefreshCommand
    {
        get { return this._refreshCommand; }
    }

    public System.Windows.Input.ICommand StopAllCommand
    {
        get { return this._stopAllCommand; }
    }

    public System.Windows.Input.ICommand ChangeRootCommand
    {
        get { return this._changeRootCommand; }
    }

    public System.Windows.Input.ICommand OpenRootCommand
    {
        get { return this._openRootCommand; }
    }

    public string RootPath
    {
        get { return this._rootPath; }
        private set { this.SetProperty(ref this._rootPath, value); }
    }

    public bool IsScanning
    {
        get { return this._isScanning; }
        private set
        {
            if (this.SetProperty(ref this._isScanning, value))
            {
                this.RaisePropertyChanged(nameof(this.IsIdle));
            }
        }
    }

    public bool IsIdle
    {
        get { return !this._isScanning; }
    }

    public string EmptyMessage
    {
        get { return this._emptyMessage; }
        private set
        {
            if (this.SetProperty(ref this._emptyMessage, value))
            {
                this.RaisePropertyChanged(nameof(this.HasEmptyMessage));
            }
        }
    }

    public bool HasEmptyMessage
    {
        get { return !String.IsNullOrEmpty(this._emptyMessage); }
    }

    public ProjectViewModel? SelectedProject
    {
        get { return this._selectedProject; }
        private set
        {
            ProjectViewModel? previous = this._selectedProject;

            if (this.SetProperty(ref this._selectedProject, value))
            {
                if (previous is not null)
                {
                    previous.IsSelected = false;
                }

                if (value is not null)
                {
                    value.IsSelected = true;
                }

                this.RaisePropertyChanged(nameof(this.HasSelectedProject));
            }
        }
    }

    public bool HasSelectedProject
    {
        get { return this._selectedProject is not null; }
    }

    public int RunningCount
    {
        get { return this._allProjects.Count(project => project.IsRunning); }
    }

    public bool HasRunning
    {
        get { return this.RunningCount > 0; }
    }

    public string RunningLabel
    {
        get
        {
            int count = this.RunningCount;
            if (count == 0)
            {
                return "nic neběží";
            }

            if (count == 1)
            {
                return "1 běžící aplikace";
            }

            if (count <= 4)
            {
                return $"{count} běžící aplikace";
            }

            return $"{count} běžících aplikací";
        }
    }

    public void Initialize()
    {
        this._pumpTimer = Application.Current?.Dispatcher.CreateTimer();

        if (this._pumpTimer is not null)
        {
            this._pumpTimer.Interval = TimeSpan.FromMilliseconds(200);
            this._pumpTimer.Tick += this.OnPumpTick;
            this._pumpTimer.Start();
        }

        this.Refresh();
    }

    public void Shutdown()
    {
        this._pumpTimer?.Stop();
        this._scanCancellation?.Cancel();

        foreach (ProjectViewModel project in this._allProjects)
        {
            project.Stop(1500);
        }
    }

    private void OnPumpTick(object? sender, EventArgs eventArgs)
    {
        foreach (ProjectViewModel project in this._allProjects)
        {
            if (project.IsRunning)
            {
                project.Pump();
            }
        }
    }

    private void Refresh()
    {
        if (this.IsScanning)
        {
            return;
        }

        this._scanCancellation?.Cancel();
        this._scanCancellation = new CancellationTokenSource();

        _ = this.RefreshAsync(this._scanCancellation.Token);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        this.IsScanning = true;
        this.EmptyMessage = String.Empty;

        try
        {
            IReadOnlyList<ScannedRepository> scanned = await this._scanner.ScanAsync(this.RootPath, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            this.ApplyScanResult(scanned);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            this.IsScanning = false;
        }
    }

    private void ApplyScanResult(IReadOnlyList<ScannedRepository> scanned)
    {
        Dictionary<string, ProjectViewModel> runningByPath = new(StringComparer.OrdinalIgnoreCase);

        foreach (ProjectViewModel project in this._allProjects)
        {
            if (project.IsRunning)
            {
                runningByPath[project.ProjectFilePath] = project;
            }
        }

        HashSet<string> reattached = new(StringComparer.OrdinalIgnoreCase);
        string? previouslySelectedPath = this.SelectedProject?.ProjectFilePath;

        this._allProjects.Clear();
        this.Repositories.Clear();
        this.SelectedProject = null;

        foreach (ScannedRepository repository in scanned)
        {
            List<SolutionViewModel> solutions = new();

            foreach (ScannedSolution solution in repository.Solutions)
            {
                List<ProjectViewModel> projects = new();

                foreach (ScannedProject project in solution.Projects)
                {
                    if (runningByPath.TryGetValue(project.ProjectFilePath, out ProjectViewModel? existing))
                    {
                        reattached.Add(project.ProjectFilePath);
                        projects.Add(existing);
                        this._allProjects.Add(existing);
                        continue;
                    }

                    ProjectViewModel viewModel = new(
                        project,
                        solution.Name,
                        this._stateStore,
                        this.OnProjectSelected,
                        this.OnRunStateChanged);

                    projects.Add(viewModel);
                    this._allProjects.Add(viewModel);
                }

                solutions.Add(new SolutionViewModel
                {
                    Name = solution.Name,
                    SolutionFilePath = solution.SolutionFilePath,
                    Projects = projects
                });
            }

            this.Repositories.Add(new RepositoryViewModel(
                repository.Name,
                repository.DirectoryPath,
                solutions,
                this._stateStore,
                this.OnShowDiff));
        }

        foreach (KeyValuePair<string, ProjectViewModel> orphan in runningByPath)
        {
            if (!reattached.Contains(orphan.Key))
            {
                orphan.Value.Stop();
            }
        }

        if (!String.IsNullOrEmpty(previouslySelectedPath))
        {
            this.SelectedProject = this._allProjects.FirstOrDefault(
                project => String.Equals(project.ProjectFilePath, previouslySelectedPath, StringComparison.OrdinalIgnoreCase));
        }

        if (this.Repositories.Count == 0)
        {
            if (!Directory.Exists(this.RootPath))
            {
                this.EmptyMessage = "Zvolená složka neexistuje.";
            }
            else
            {
                this.EmptyMessage = "Nenašel jsem žádné spustitelné projekty. Hledám .sln a .slnx ve složce src/ každého repozitáře.";
            }
        }

        this.OnRunStateChanged();
    }

    private void OnProjectSelected(ProjectViewModel project)
    {
        this.SelectedProject = project;
    }

    private void OnShowDiff(RepositoryViewModel repository)
    {
        this.GitDiff.Open(repository.Name, repository.DirectoryPath);
    }

    private void OnRunStateChanged()
    {
        this.RaisePropertyChanged(nameof(this.RunningCount));
        this.RaisePropertyChanged(nameof(this.HasRunning));
        this.RaisePropertyChanged(nameof(this.RunningLabel));

        foreach (RepositoryViewModel repository in this.Repositories)
        {
            repository.NotifyRunStateChanged();
        }
    }

    private void StopAll()
    {
        foreach (ProjectViewModel project in this._allProjects)
        {
            project.Stop();
        }
    }

    private void OpenRoot()
    {
        if (!Directory.Exists(this.RootPath))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = this.RootPath,
            UseShellExecute = true
        });
    }

    private void ChangeRoot()
    {
        _ = this.ChangeRootAsync();
    }

    private async Task ChangeRootAsync()
    {
        try
        {
            string? selectedPath = await FolderPickerService.PickFolderAsync();

            if (!String.IsNullOrEmpty(selectedPath))
            {
                this.RootPath = selectedPath;
                this._stateStore.RepositoriesRoot = selectedPath;
                this.Refresh();
            }
        }
        catch (Exception)
        {
        }
    }
}
