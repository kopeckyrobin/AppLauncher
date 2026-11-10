using AppLauncher.Services;

namespace AppLauncher.ViewModels;

public sealed class RepositoryViewModel : ObservableBase
{
    private readonly AppStateStore _stateStore;
    private readonly Action<RepositoryViewModel> _onShowDiff;
    private readonly Action<RepositoryViewModel> _onVisibilityChanged;
    private readonly RelayCommand _hideCommand;
    private readonly RelayCommand _unhideCommand;
    private readonly RelayCommand _toggleExpandCommand;
    private readonly RelayCommand _showDiffCommand;
    private readonly RelayCommand _openFolderCommand;
    private readonly RelayCommand _openTerminalCommand;
    private bool _isExpanded;
    private string _currentBranch = String.Empty;

    public RepositoryViewModel(
        string name,
        string directoryPath,
        IReadOnlyList<SolutionViewModel> solutions,
        AppStateStore stateStore,
        Action<RepositoryViewModel> onShowDiff,
        Action<RepositoryViewModel> onVisibilityChanged)
    {
        this.Name = name;
        this.DirectoryPath = directoryPath;
        this.Solutions = solutions;
        this._stateStore = stateStore;
        this._onShowDiff = onShowDiff;
        this._onVisibilityChanged = onVisibilityChanged;
        this._hideCommand = new RelayCommand(this.Hide, () => !this.HasRunning);
        this._unhideCommand = new RelayCommand(this.Unhide);
        this._isExpanded = !stateStore.IsCollapsed(directoryPath);
        this._toggleExpandCommand = new RelayCommand(this.ToggleExpand);
        this._showDiffCommand = new RelayCommand(this.ShowDiff);
        this._openFolderCommand = new RelayCommand(this.OpenFolder);
        this._openTerminalCommand = new RelayCommand(this.OpenTerminal);
        this.HasGit = GitService.IsRepository(directoryPath);
        this.RefreshBranch();

        this.Projects = solutions.SelectMany(solution => solution.Projects).ToList();
    }

    public bool HasGit { get; }

    public string CurrentBranch
    {
        get { return this._currentBranch; }
        private set
        {
            if (this.SetProperty(ref this._currentBranch, value))
            {
                this.RaisePropertyChanged(nameof(this.HasBranch));
            }
        }
    }

    public bool HasBranch
    {
        get { return !String.IsNullOrEmpty(this.CurrentBranch); }
    }

    public void RefreshBranch()
    {
        this.CurrentBranch = this.HasGit ? GitService.ReadCurrentBranch(this.DirectoryPath) : String.Empty;
    }

    public string Name { get; }

    public string DirectoryPath { get; }

    public IReadOnlyList<SolutionViewModel> Solutions { get; }

    public IReadOnlyList<ProjectViewModel> Projects { get; }

    public System.Windows.Input.ICommand ToggleExpandCommand
    {
        get { return this._toggleExpandCommand; }
    }

    public System.Windows.Input.ICommand ShowDiffCommand
    {
        get { return this._showDiffCommand; }
    }

    public System.Windows.Input.ICommand OpenFolderCommand
    {
        get { return this._openFolderCommand; }
    }

    public System.Windows.Input.ICommand OpenTerminalCommand
    {
        get { return this._openTerminalCommand; }
    }

    public bool IsExpanded
    {
        get { return this._isExpanded; }
        set
        {
            if (this.SetProperty(ref this._isExpanded, value))
            {
                this._stateStore.SetCollapsed(this.DirectoryPath, !value);
                this.RaisePropertyChanged(nameof(this.ExpanderGlyph));
            }
        }
    }

    public string ExpanderGlyph
    {
        get
        {
            if (this._isExpanded)
            {
                return "▾";
            }

            return "▸";
        }
    }

    public bool IsHidden
    {
        get { return this._stateStore.IsHidden(this.DirectoryPath); }
    }

    public System.Windows.Input.ICommand HideCommand
    {
        get { return this._hideCommand; }
    }

    public System.Windows.Input.ICommand UnhideCommand
    {
        get { return this._unhideCommand; }
    }

    public string ProjectCountLabel
    {
        get
        {
            int count = this.Projects.Count;
            if (count == 1)
            {
                return "1 projekt";
            }

            if (count >= 2 && count <= 4)
            {
                return $"{count} projekty";
            }

            return $"{count} projektů";
        }
    }

    public int RunningCount
    {
        get { return this.Projects.Count(project => project.IsRunning); }
    }

    public bool HasRunning
    {
        get { return this.RunningCount > 0; }
    }

    public void NotifyRunStateChanged()
    {
        this.RaisePropertyChanged(nameof(this.RunningCount));
        this.RaisePropertyChanged(nameof(this.HasRunning));
        this._hideCommand.RaiseCanExecuteChanged();
    }

    private void Hide()
    {
        this._stateStore.SetHidden(this.DirectoryPath, true);
        this._onVisibilityChanged(this);
    }

    private void Unhide()
    {
        this._stateStore.SetHidden(this.DirectoryPath, false);
        this._onVisibilityChanged(this);
    }

    private void ToggleExpand()
    {
        this.IsExpanded = !this.IsExpanded;
    }

    private void ShowDiff()
    {
        this._onShowDiff(this);
    }

    private void OpenFolder()
    {
        ShellService.OpenFolder(this.DirectoryPath);
    }

    private void OpenTerminal()
    {
        ShellService.OpenTerminal(this.DirectoryPath);
    }
}
