namespace AppLauncher.Models;

public enum DiffLineKind
{
    Context,
    Added,
    Removed,
    Hunk,
    Filler
}

public enum GitDiffSourceKind
{
    WorkingTree,
    Commit
}

public sealed class GitCommit
{
    public required string Sha { get; init; }

    public required string ShortSha { get; init; }

    public required string Subject { get; init; }
}

public sealed class GitDiffSource
{
    private const int SubjectLength = 20;

    public required GitDiffSourceKind Kind { get; init; }

    public string? Sha { get; init; }

    public required string DisplayName { get; init; }

    public static GitDiffSource WorkingTree
    {
        get
        {
            return new GitDiffSource
            {
                Kind = GitDiffSourceKind.WorkingTree,
                DisplayName = "Current Changes"
            };
        }
    }

    public static GitDiffSource FromCommit(GitCommit commit)
    {
        string subject = commit.Subject.Replace('\t', ' ').Trim();

        if (subject.Length > SubjectLength)
        {
            subject = subject[..SubjectLength].TrimEnd();
        }

        return new GitDiffSource
        {
            Kind = GitDiffSourceKind.Commit,
            Sha = commit.Sha,
            DisplayName = $"{subject}  {commit.ShortSha}"
        };
    }

    public override string ToString()
    {
        return this.DisplayName;
    }
}

public sealed class GitFileChange
{
    public required string Path { get; init; }

    public required string StatusCode { get; init; }

    public required bool IsUntracked { get; init; }

    public string? OriginalPath { get; init; }

    public string FileName
    {
        get
        {
            int separator = this.Path.LastIndexOf('/');
            if (separator < 0)
            {
                return this.Path;
            }

            return this.Path[(separator + 1)..];
        }
    }

    public string DirectoryName
    {
        get
        {
            int separator = this.Path.LastIndexOf('/');
            if (separator < 0)
            {
                return String.Empty;
            }

            return this.Path[..separator];
        }
    }
}

public sealed class DiffLine
{
    public required DiffLineKind Kind { get; init; }

    public required string Text { get; init; }

    public string OldNumber { get; init; } = String.Empty;

    public string NewNumber { get; init; } = String.Empty;

    public bool IsMatch { get; set; }

    public string Sign
    {
        get
        {
            switch (this.Kind)
            {
                case DiffLineKind.Added:
                    return "+";
                case DiffLineKind.Removed:
                    return "-";
                default:
                    return " ";
            }
        }
    }

    public bool IsHunk
    {
        get { return this.Kind == DiffLineKind.Hunk; }
    }
}

public sealed class DiffRow
{
    public required DiffLineKind LeftKind { get; init; }

    public required DiffLineKind RightKind { get; init; }

    public string LeftNumber { get; init; } = String.Empty;

    public string LeftText { get; init; } = String.Empty;

    public string RightNumber { get; init; } = String.Empty;

    public string RightText { get; init; } = String.Empty;

    public bool LeftIsMatch { get; set; }

    public bool RightIsMatch { get; set; }

    public bool IsHunk
    {
        get { return this.LeftKind == DiffLineKind.Hunk; }
    }
}

public sealed class ChangeMarker
{
    public required double Start { get; init; }

    public required double End { get; init; }

    public required bool IsAddition { get; init; }
}

public sealed class DiffDocument
{
    public required IReadOnlyList<DiffLine> InlineLines { get; init; }

    public required IReadOnlyList<DiffRow> SideRows { get; init; }

    public required int AddedCount { get; init; }

    public required int RemovedCount { get; init; }

    public required IReadOnlyList<ChangeMarker> Markers { get; init; }

    public bool IsTruncated { get; init; }

    public static DiffDocument Empty
    {
        get
        {
            return new DiffDocument
            {
                InlineLines = Array.Empty<DiffLine>(),
                SideRows = Array.Empty<DiffRow>(),
                AddedCount = 0,
                RemovedCount = 0,
                Markers = Array.Empty<ChangeMarker>()
            };
        }
    }
}
