using AppLauncher.Services;

namespace AppLauncher.ViewModels;

public sealed class RepositoryViewModel : ObservableBase
{
    private readonly AppStateStore _stateStore;
    private readonly Action<RepositoryViewModel> _onShowDiff;
    private readonly RelayCommand _toggleExpandCommand;
    private readonly RelayCommand _showDiffCommand;
    private bool _isExpanded;

    public RepositoryViewModel(
        string name,
        string directoryPath,
        IReadOnlyList<SolutionViewModel> solutions,
        AppStateStore stateStore,
        Action<RepositoryViewModel> onShowDiff)
    {
        this.Name = name;
        this.DirectoryPath = directoryPath;
        this.Solutions = solutions;
        this._stateStore = stateStore;
        this._onShowDiff = onShowDiff;
        this._isExpanded = !stateStore.IsCollapsed(directoryPath);
        this._toggleExpandCommand = new RelayCommand(this.ToggleExpand);
        this._showDiffCommand = new RelayCommand(this.ShowDiff);
        this.HasGit = GitService.IsRepository(directoryPath);
        this.CurrentBranch = this.HasGit ? GitService.ReadCurrentBranch(directoryPath) : String.Empty;

        this.Projects = solutions.SelectMany(solution => solution.Projects).ToList();
    }

    public bool HasGit { get; }

    public string CurrentBranch { get; }

    public bool HasBranch
    {
        get { return !String.IsNullOrEmpty(this.CurrentBranch); }
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
    }

    private void ToggleExpand()
    {
        this.IsExpanded = !this.IsExpanded;
    }

    private void ShowDiff()
    {
        this._onShowDiff(this);
    }
}
