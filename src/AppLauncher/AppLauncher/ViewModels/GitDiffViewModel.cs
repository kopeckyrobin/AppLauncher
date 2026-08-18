using System.Collections.ObjectModel;
using AppLauncher.Models;
using AppLauncher.Services;

namespace AppLauncher.ViewModels;

public sealed class GitDiffViewModel : ObservableBase
{
    private readonly GitService _gitService = new();
    private readonly RelayCommand _closeCommand;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _showInlineCommand;
    private readonly RelayCommand _showSideBySideCommand;

    private const int CommitCount = 50;

    private CancellationTokenSource? _cancellation;
    private GitDiffSource? _selectedSource;
    private GitFileViewModel? _selectedFile;
    private string? _selectedCommitParent;
    private bool _isSwitchingSource;
    private IReadOnlyList<DiffLine> _inlineLines = Array.Empty<DiffLine>();
    private IReadOnlyList<DiffRow> _sideRows = Array.Empty<DiffRow>();
    private IReadOnlyList<ChangeMarker> _markers = Array.Empty<ChangeMarker>();
    private string _repositoryName = String.Empty;
    private string _repositoryPath = String.Empty;
    private string _errorMessage = String.Empty;
    private string _diffSummary = String.Empty;
    private bool _isOpen;
    private bool _isLoading;
    private bool _isSideBySide;
    private bool _isTruncated;

    public GitDiffViewModel()
    {
        this._closeCommand = new RelayCommand(this.Close);
        this._refreshCommand = new RelayCommand(this.Refresh);
        this._showInlineCommand = new RelayCommand(this.ShowInline);
        this._showSideBySideCommand = new RelayCommand(this.ShowSideBySide);
    }

    public ObservableCollection<GitFileViewModel> Files { get; } = new();

    public ObservableCollection<GitDiffSource> Sources { get; } = new();

    public GitDiffSource? SelectedSource
    {
        get { return this._selectedSource; }
        set
        {
            if (value is null)
            {
                return;
            }

            if (this.SetProperty(ref this._selectedSource, value) && !this._isSwitchingSource)
            {
                this.ReloadFiles();
            }
        }
    }

    public System.Windows.Input.ICommand CloseCommand
    {
        get { return this._closeCommand; }
    }

    public System.Windows.Input.ICommand RefreshCommand
    {
        get { return this._refreshCommand; }
    }

    public System.Windows.Input.ICommand ShowInlineCommand
    {
        get { return this._showInlineCommand; }
    }

    public System.Windows.Input.ICommand ShowSideBySideCommand
    {
        get { return this._showSideBySideCommand; }
    }

    public bool IsOpen
    {
        get { return this._isOpen; }
        private set { this.SetProperty(ref this._isOpen, value); }
    }

    public bool IsLoading
    {
        get { return this._isLoading; }
        private set { this.SetProperty(ref this._isLoading, value); }
    }

    public string RepositoryName
    {
        get { return this._repositoryName; }
        private set { this.SetProperty(ref this._repositoryName, value); }
    }

    public string ErrorMessage
    {
        get { return this._errorMessage; }
        private set
        {
            if (this.SetProperty(ref this._errorMessage, value))
            {
                this.RaisePropertyChanged(nameof(this.HasError));
            }
        }
    }

    public bool HasError
    {
        get { return !String.IsNullOrEmpty(this._errorMessage); }
    }

    public bool IsSideBySide
    {
        get { return this._isSideBySide; }
        private set
        {
            if (this.SetProperty(ref this._isSideBySide, value))
            {
                this.RaisePropertyChanged(nameof(this.IsInline));
            }
        }
    }

    public bool IsInline
    {
        get { return !this._isSideBySide; }
    }

    public IReadOnlyList<DiffLine> InlineLines
    {
        get { return this._inlineLines; }
        private set { this.SetProperty(ref this._inlineLines, value); }
    }

    public IReadOnlyList<DiffRow> SideRows
    {
        get { return this._sideRows; }
        private set { this.SetProperty(ref this._sideRows, value); }
    }

    public IReadOnlyList<ChangeMarker> Markers
    {
        get { return this._markers; }
        private set { this.SetProperty(ref this._markers, value); }
    }

    public GitFileViewModel? SelectedFile
    {
        get { return this._selectedFile; }
        private set
        {
            GitFileViewModel? previous = this._selectedFile;

            if (this.SetProperty(ref this._selectedFile, value))
            {
                if (previous is not null)
                {
                    previous.IsSelected = false;
                }

                if (value is not null)
                {
                    value.IsSelected = true;
                }

                this.RaisePropertyChanged(nameof(this.HasSelectedFile));
                this.RaisePropertyChanged(nameof(this.SelectedPath));
            }
        }
    }

    public bool HasSelectedFile
    {
        get { return this._selectedFile is not null; }
    }

    public string SelectedPath
    {
        get
        {
            if (this._selectedFile is null)
            {
                return String.Empty;
            }

            return this._selectedFile.Change.Path;
        }
    }

    public string DiffSummary
    {
        get { return this._diffSummary; }
        private set
        {
            if (this.SetProperty(ref this._diffSummary, value))
            {
                this.RaisePropertyChanged(nameof(this.HasDiffSummary));
            }
        }
    }

    public bool HasDiffSummary
    {
        get { return !String.IsNullOrEmpty(this._diffSummary); }
    }

    public bool IsTruncated
    {
        get { return this._isTruncated; }
        private set { this.SetProperty(ref this._isTruncated, value); }
    }

    public string FileCountLabel
    {
        get
        {
            int count = this.Files.Count;

            if (count == 1)
            {
                return "1 změněný soubor";
            }

            if (count >= 2 && count <= 4)
            {
                return $"{count} změněné soubory";
            }

            return $"{count} změněných souborů";
        }
    }

    public bool HasNoChanges
    {
        get { return !this.IsLoading && !this.HasError && this.Files.Count == 0; }
    }

    public string EmptyListMessage
    {
        get
        {
            if (this._selectedSource is not null && this._selectedSource.Kind == GitDiffSourceKind.Commit)
            {
                return "Commit neobsahuje žádné změny.";
            }

            return "Pracovní strom je čistý.";
        }
    }

    public void Open(string repositoryName, string repositoryPath)
    {
        this.RepositoryName = repositoryName;
        this._repositoryPath = repositoryPath;
        this.IsOpen = true;
        this.Refresh();
    }

    public void Close()
    {
        this._cancellation?.Cancel();
        this.IsOpen = false;
        this._isSwitchingSource = true;
        this.Sources.Clear();
        this.SelectedSource = null;
        this._selectedSource = null;
        this._isSwitchingSource = false;
        this.Files.Clear();
        this.SelectedFile = null;
        this.InlineLines = Array.Empty<DiffLine>();
        this.SideRows = Array.Empty<DiffRow>();
        this.Markers = Array.Empty<ChangeMarker>();
        this.DiffSummary = String.Empty;
        this.ErrorMessage = String.Empty;
    }

    private void ShowInline()
    {
        this.IsSideBySide = false;
    }

    private void ShowSideBySide()
    {
        this.IsSideBySide = true;
    }

    private void Refresh()
    {
        this._cancellation?.Cancel();
        this._cancellation = new CancellationTokenSource();

        _ = this.ReloadAllAsync(this._cancellation.Token);
    }

    private void ReloadFiles()
    {
        this._cancellation?.Cancel();
        this._cancellation = new CancellationTokenSource();

        _ = this.LoadFilesAsync(this._cancellation.Token);
    }

    private async Task ReloadAllAsync(CancellationToken cancellationToken)
    {
        this.ErrorMessage = String.Empty;

        try
        {
            IReadOnlyList<GitCommit> commits = await this._gitService.GetCommitsAsync(
                this._repositoryPath,
                CommitCount,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            string? previousSha = this._selectedSource?.Sha;
            bool wasCommit = this._selectedSource is not null && this._selectedSource.Kind == GitDiffSourceKind.Commit;

            this._isSwitchingSource = true;

            this.Sources.Clear();
            this.Sources.Add(GitDiffSource.WorkingTree);

            foreach (GitCommit commit in commits)
            {
                this.Sources.Add(GitDiffSource.FromCommit(commit));
            }

            GitDiffSource restored = this.Sources[0];

            if (wasCommit && !String.IsNullOrEmpty(previousSha))
            {
                restored = this.Sources.FirstOrDefault(
                    source => String.Equals(source.Sha, previousSha, StringComparison.Ordinal)) ?? this.Sources[0];
            }

            this.SelectedSource = restored;
            this._isSwitchingSource = false;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            this._isSwitchingSource = false;
            this.ErrorMessage = exception.Message;
        }

        await this.LoadFilesAsync(cancellationToken);
    }

    private async Task LoadFilesAsync(CancellationToken cancellationToken)
    {
        this.IsLoading = true;
        this.ErrorMessage = String.Empty;
        this.Files.Clear();
        this.SelectedFile = null;
        this.InlineLines = Array.Empty<DiffLine>();
        this.SideRows = Array.Empty<DiffRow>();
        this.Markers = Array.Empty<ChangeMarker>();
        this.DiffSummary = String.Empty;
        this.RaisePropertyChanged(nameof(this.EmptyListMessage));

        try
        {
            GitDiffSource source = this._selectedSource ?? GitDiffSource.WorkingTree;

            IReadOnlyList<GitFileChange> changes;

            if (source.Kind == GitDiffSourceKind.Commit && !String.IsNullOrEmpty(source.Sha))
            {
                this._selectedCommitParent = await this._gitService.GetFirstParentAsync(
                    this._repositoryPath,
                    source.Sha,
                    cancellationToken);

                changes = await this._gitService.GetCommitFilesAsync(
                    this._repositoryPath,
                    source.Sha,
                    this._selectedCommitParent,
                    cancellationToken);
            }
            else
            {
                this._selectedCommitParent = null;
                changes = await this._gitService.GetChangedFilesAsync(this._repositoryPath, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (GitFileChange change in changes)
            {
                this.Files.Add(new GitFileViewModel(change, this.OnFileSelected));
            }

            this.RaisePropertyChanged(nameof(this.FileCountLabel));

            if (this.Files.Count > 0)
            {
                this.OnFileSelected(this.Files[0]);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            this.ErrorMessage = exception.Message;
        }
        finally
        {
            this.IsLoading = false;
            this.RaisePropertyChanged(nameof(this.HasNoChanges));
        }
    }

    private void OnFileSelected(GitFileViewModel file)
    {
        this.SelectedFile = file;
        _ = this.LoadDiffAsync(file);
    }

    private async Task LoadDiffAsync(GitFileViewModel file)
    {
        try
        {
            GitDiffSource source = this._selectedSource ?? GitDiffSource.WorkingTree;

            DiffDocument document;

            if (source.Kind == GitDiffSourceKind.Commit && !String.IsNullOrEmpty(source.Sha))
            {
                document = await this._gitService.GetCommitDiffAsync(
                    this._repositoryPath,
                    source.Sha,
                    this._selectedCommitParent,
                    file.Change,
                    CancellationToken.None);
            }
            else
            {
                document = await this._gitService.GetDiffAsync(
                    this._repositoryPath,
                    file.Change,
                    CancellationToken.None);
            }

            if (!ReferenceEquals(this.SelectedFile, file))
            {
                return;
            }

            this.InlineLines = document.InlineLines;
            this.SideRows = document.SideRows;
            this.Markers = document.Markers;
            this.IsTruncated = document.IsTruncated;

            if (document.InlineLines.Count == 0)
            {
                this.DiffSummary = String.Empty;
            }
            else
            {
                this.DiffSummary = $"+{document.AddedCount}  −{document.RemovedCount}";
            }
        }
        catch (Exception exception)
        {
            this.ErrorMessage = exception.Message;
        }
    }
}
