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
    private readonly RelayCommand _commitAndPushCommand;
    private readonly RelayCommand _findNextCommand;
    private readonly RelayCommand _findPreviousCommand;
    private readonly List<GitFileViewModel> _allFiles = new();
    private readonly List<int> _inlineMatches = new();
    private readonly List<int> _sideMatches = new();

    private const int CommitCount = 50;

    private CancellationTokenSource? _cancellation;
    private DiffDocument? _document;
    private string _branchName = String.Empty;
    private string _fileFilter = String.Empty;
    private string _diffSearchText = String.Empty;
    private int _matchPosition = -1;
    private string _commitMessage = String.Empty;
    private string _commitStatus = String.Empty;
    private bool _commitFailed;
    private bool _isCommitting;
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
        this._commitAndPushCommand = new RelayCommand(this.CommitAndPush, this.CanCommitAndPush);
        this._findNextCommand = new RelayCommand(this.FindNext, this.HasMatches);
        this._findPreviousCommand = new RelayCommand(this.FindPrevious, this.HasMatches);
    }

    public event EventHandler<int>? MatchScrollRequested;

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

            if (this.SetProperty(ref this._selectedSource, value))
            {
                this.RaisePropertyChanged(nameof(this.IsWorkingTree));
                this._commitAndPushCommand.RaiseCanExecuteChanged();

                if (!this._isSwitchingSource)
                {
                    this.ReloadFiles();
                }
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

    public System.Windows.Input.ICommand CommitAndPushCommand
    {
        get { return this._commitAndPushCommand; }
    }

    public System.Windows.Input.ICommand FindNextCommand
    {
        get { return this._findNextCommand; }
    }

    public System.Windows.Input.ICommand FindPreviousCommand
    {
        get { return this._findPreviousCommand; }
    }

    public string BranchName
    {
        get { return this._branchName; }
        private set
        {
            if (this.SetProperty(ref this._branchName, value))
            {
                this.RaisePropertyChanged(nameof(this.HasBranch));
            }
        }
    }

    public bool HasBranch
    {
        get { return !String.IsNullOrEmpty(this._branchName); }
    }

    public string FileFilter
    {
        get { return this._fileFilter; }
        set
        {
            if (this.SetProperty(ref this._fileFilter, value ?? String.Empty))
            {
                this.ApplyFileFilter();
            }
        }
    }

    public string DiffSearchText
    {
        get { return this._diffSearchText; }
        set
        {
            if (this.SetProperty(ref this._diffSearchText, value ?? String.Empty))
            {
                this.RaisePropertyChanged(nameof(this.HasSearchText));
                this.ApplySearch(true);
            }
        }
    }

    public bool HasSearchText
    {
        get { return !String.IsNullOrEmpty(this._diffSearchText); }
    }

    public string MatchLabel
    {
        get
        {
            if (!this.HasSearchText)
            {
                return String.Empty;
            }

            int count = this.ActiveMatches.Count;

            if (count == 0)
            {
                return "žádná shoda";
            }

            return $"{this._matchPosition + 1} / {count}";
        }
    }

    public bool IsWorkingTree
    {
        get { return this._selectedSource is null || this._selectedSource.Kind == GitDiffSourceKind.WorkingTree; }
    }

    public string CommitMessage
    {
        get { return this._commitMessage; }
        set
        {
            if (this.SetProperty(ref this._commitMessage, value ?? String.Empty))
            {
                this._commitAndPushCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCommitting
    {
        get { return this._isCommitting; }
        private set
        {
            if (this.SetProperty(ref this._isCommitting, value))
            {
                this.RaisePropertyChanged(nameof(this.CommitButtonText));
                this._commitAndPushCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CommitButtonText
    {
        get
        {
            if (this._isCommitting)
            {
                return "Odesílám…";
            }

            return "Commit and Push";
        }
    }

    public string CommitStatus
    {
        get { return this._commitStatus; }
        private set
        {
            if (this.SetProperty(ref this._commitStatus, value))
            {
                this.RaisePropertyChanged(nameof(this.HasCommitStatus));
            }
        }
    }

    public bool HasCommitStatus
    {
        get { return !String.IsNullOrEmpty(this._commitStatus); }
    }

    public bool CommitFailed
    {
        get { return this._commitFailed; }
        private set { this.SetProperty(ref this._commitFailed, value); }
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
                this.ResetMatchPosition();
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
            int count = this._allFiles.Count;

            if (!String.IsNullOrEmpty(this._fileFilter))
            {
                return $"{this.Files.Count} z {count} souborů";
            }

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
            if (!String.IsNullOrEmpty(this._fileFilter))
            {
                return "Žádný soubor neodpovídá hledání.";
            }

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
        this.FileFilter = String.Empty;
        this.DiffSearchText = String.Empty;
        this.CommitMessage = String.Empty;
        this.CommitStatus = String.Empty;
        this.CommitFailed = false;
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
        this._allFiles.Clear();
        this.Files.Clear();
        this.SelectedFile = null;
        this._document = null;
        this.InlineLines = Array.Empty<DiffLine>();
        this.SideRows = Array.Empty<DiffRow>();
        this.Markers = Array.Empty<ChangeMarker>();
        this.DiffSummary = String.Empty;
        this.ErrorMessage = String.Empty;
    }

    private List<int> ActiveMatches
    {
        get { return this._isSideBySide ? this._sideMatches : this._inlineMatches; }
    }

    private bool HasMatches()
    {
        return this.ActiveMatches.Count > 0;
    }

    private void ApplyFileFilter()
    {
        GitFileViewModel? previous = this.SelectedFile;

        this.Files.Clear();

        foreach (GitFileViewModel file in this._allFiles)
        {
            if (String.IsNullOrEmpty(this._fileFilter) ||
                file.Change.Path.Contains(this._fileFilter, StringComparison.OrdinalIgnoreCase))
            {
                this.Files.Add(file);
            }
        }

        this.RaisePropertyChanged(nameof(this.FileCountLabel));
        this.RaisePropertyChanged(nameof(this.EmptyListMessage));
        this.RaisePropertyChanged(nameof(this.HasNoChanges));

        if (this.Files.Count == 0)
        {
            return;
        }

        if (previous is null || !this.Files.Contains(previous))
        {
            this.OnFileSelected(this.Files[0]);
        }
    }

    private void ApplySearch(bool jumpToFirst)
    {
        this._inlineMatches.Clear();
        this._sideMatches.Clear();

        DiffDocument? document = this._document;

        if (document is null)
        {
            this.ResetMatchPosition();
            return;
        }

        string search = this._diffSearchText;
        bool hasSearch = !String.IsNullOrEmpty(search);

        for (int index = 0; index < document.InlineLines.Count; index++)
        {
            DiffLine line = document.InlineLines[index];
            bool isMatch = hasSearch && !line.IsHunk && line.Text.Contains(search, StringComparison.OrdinalIgnoreCase);

            line.IsMatch = isMatch;

            if (isMatch)
            {
                this._inlineMatches.Add(index);
            }
        }

        for (int index = 0; index < document.SideRows.Count; index++)
        {
            DiffRow row = document.SideRows[index];
            bool leftMatch = hasSearch && !row.IsHunk && row.LeftText.Contains(search, StringComparison.OrdinalIgnoreCase);
            bool rightMatch = hasSearch && !row.IsHunk && row.RightText.Contains(search, StringComparison.OrdinalIgnoreCase);

            row.LeftIsMatch = leftMatch;
            row.RightIsMatch = rightMatch;

            if (leftMatch || rightMatch)
            {
                this._sideMatches.Add(index);
            }
        }

        this.InlineLines = new List<DiffLine>(document.InlineLines);
        this.SideRows = new List<DiffRow>(document.SideRows);

        this.ResetMatchPosition();

        if (jumpToFirst)
        {
            this.RequestScroll();
        }
    }

    private void ResetMatchPosition()
    {
        this._matchPosition = this.ActiveMatches.Count > 0 ? 0 : -1;
        this.RaisePropertyChanged(nameof(this.MatchLabel));
        this._findNextCommand.RaiseCanExecuteChanged();
        this._findPreviousCommand.RaiseCanExecuteChanged();
    }

    private void FindNext()
    {
        List<int> matches = this.ActiveMatches;

        if (matches.Count == 0)
        {
            return;
        }

        this._matchPosition = (this._matchPosition + 1) % matches.Count;
        this.RaisePropertyChanged(nameof(this.MatchLabel));
        this.RequestScroll();
    }

    private void FindPrevious()
    {
        List<int> matches = this.ActiveMatches;

        if (matches.Count == 0)
        {
            return;
        }

        this._matchPosition = (this._matchPosition - 1 + matches.Count) % matches.Count;
        this.RaisePropertyChanged(nameof(this.MatchLabel));
        this.RequestScroll();
    }

    private void RequestScroll()
    {
        List<int> matches = this.ActiveMatches;

        if (this._matchPosition < 0 || this._matchPosition >= matches.Count)
        {
            return;
        }

        this.MatchScrollRequested?.Invoke(this, matches[this._matchPosition]);
    }

    private bool CanCommitAndPush()
    {
        return !this._isCommitting
            && this.IsWorkingTree
            && this._allFiles.Count > 0
            && !String.IsNullOrEmpty(this._commitMessage.Trim());
    }

    private void CommitAndPush()
    {
        _ = this.CommitAndPushAsync();
    }

    private async Task CommitAndPushAsync()
    {
        this.IsCommitting = true;
        this.CommitFailed = false;
        this.CommitStatus = String.Empty;

        try
        {
            string branch = await this._gitService.CommitAndPushAsync(
                this._repositoryPath,
                this._commitMessage.Trim(),
                CancellationToken.None);

            this.CommitMessage = String.Empty;
            this.CommitStatus = $"Odesláno do origin/{branch}";
            this.Refresh();
        }
        catch (Exception exception)
        {
            this.CommitFailed = true;
            this.CommitStatus = Condense(exception.Message);
        }
        finally
        {
            this.IsCommitting = false;
        }
    }

    private static string Condense(string message)
    {
        List<string> parts = new();

        foreach (string line in message.Split('\n'))
        {
            string trimmed = line.Trim();

            if (!String.IsNullOrEmpty(trimmed))
            {
                parts.Add(trimmed);
            }
        }

        string joined = String.Join(" · ", parts);

        if (joined.Length > 200)
        {
            return joined[..200] + "…";
        }

        return joined;
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
            string branch = await this._gitService.GetCurrentBranchAsync(this._repositoryPath, cancellationToken);

            this.BranchName = String.Equals(branch, "HEAD", StringComparison.Ordinal) ? "detached HEAD" : branch;

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
        this._allFiles.Clear();
        this.Files.Clear();
        this.SelectedFile = null;
        this._document = null;
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
                this._allFiles.Add(new GitFileViewModel(change, this.OnFileSelected));
            }

            this.ApplyFileFilter();
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
            this._commitAndPushCommand.RaiseCanExecuteChanged();
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

            this._document = document;
            this.Markers = document.Markers;
            this.IsTruncated = document.IsTruncated;
            this.ApplySearch(false);

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
