using System.Diagnostics;
using System.Text;
using AppLauncher.Models;

namespace AppLauncher.Services;

public sealed class GitCommandResult
{
    public required bool Success { get; init; }

    public required string Output { get; init; }

    public required string Error { get; init; }
}

public sealed class GitService
{
    private const int MaximumFiles = 500;
    private const int MaximumUntrackedBytes = 400_000;

    public static bool IsRepository(string directoryPath)
    {
        string gitPath = Path.Combine(directoryPath, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    public static string ReadCurrentBranch(string directoryPath)
    {
        string? gitDirectory = ResolveGitDirectory(directoryPath);

        if (String.IsNullOrEmpty(gitDirectory))
        {
            return String.Empty;
        }

        string headPath = Path.Combine(gitDirectory, "HEAD");

        if (!File.Exists(headPath))
        {
            return String.Empty;
        }

        string head;

        try
        {
            head = File.ReadAllText(headPath).Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return String.Empty;
        }

        const string branchPrefix = "ref: refs/heads/";

        if (head.StartsWith(branchPrefix, StringComparison.Ordinal))
        {
            return head[branchPrefix.Length..];
        }

        if (head.Length >= 7)
        {
            return head[..7];
        }

        return String.Empty;
    }

    private static string? ResolveGitDirectory(string directoryPath)
    {
        string gitPath = Path.Combine(directoryPath, ".git");

        if (Directory.Exists(gitPath))
        {
            return gitPath;
        }

        if (!File.Exists(gitPath))
        {
            return null;
        }

        string content;

        try
        {
            content = File.ReadAllText(gitPath).Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        const string directoryPrefix = "gitdir:";

        if (!content.StartsWith(directoryPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string target = content[directoryPrefix.Length..].Trim();

        if (!Path.IsPathRooted(target))
        {
            target = Path.GetFullPath(Path.Combine(directoryPath, target));
        }

        return Directory.Exists(target) ? target : null;
    }

    public async Task<IReadOnlyList<GitFileChange>> GetChangedFilesAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunAsync(
            repositoryPath,
            new[] { "status", "--porcelain", "-z", "-uall" },
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(String.IsNullOrEmpty(result.Error) ? "git status selhal." : result.Error);
        }

        List<GitFileChange> changes = new();
        string[] records = result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < records.Length; index++)
        {
            string record = records[index];
            if (record.Length < 4)
            {
                continue;
            }

            string status = record[..2];
            string path = record[3..];
            string? originalPath = null;

            if (status[0] is 'R' or 'C')
            {
                if (index + 1 < records.Length)
                {
                    index++;
                    originalPath = records[index];
                }
            }

            changes.Add(new GitFileChange
            {
                Path = path,
                StatusCode = NormalizeStatus(status),
                IsUntracked = status == "??",
                OriginalPath = originalPath
            });

            if (changes.Count >= MaximumFiles)
            {
                break;
            }
        }

        changes.Sort((left, right) => String.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
        return changes;
    }

    public async Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string repositoryPath, int count, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunAsync(
            repositoryPath,
            new[] { "log", "--max-count=" + count.ToString(), "--pretty=format:%H%x1f%h%x1f%s%x1e" },
            cancellationToken);

        if (!result.Success)
        {
            return Array.Empty<GitCommit>();
        }

        List<GitCommit> commits = new();

        foreach (string record in result.Output.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = record.Trim('\n', '\r').Split('\u001f');
            if (fields.Length < 3)
            {
                continue;
            }

            commits.Add(new GitCommit
            {
                Sha = fields[0],
                ShortSha = fields[1],
                Subject = fields[2]
            });
        }

        return commits;
    }

    public async Task<string?> GetFirstParentAsync(string repositoryPath, string sha, CancellationToken cancellationToken)
    {
        GitCommandResult result = await this.RunAsync(
            repositoryPath,
            new[] { "rev-list", "--parents", "-n", "1", sha },
            cancellationToken);

        if (!result.Success)
        {
            return null;
        }

        string[] parts = result.Output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return null;
        }

        return parts[1];
    }

    public async Task<IReadOnlyList<GitFileChange>> GetCommitFilesAsync(
        string repositoryPath,
        string sha,
        string? firstParent,
        CancellationToken cancellationToken)
    {
        string[] arguments = String.IsNullOrEmpty(firstParent)
            ? new[] { "diff-tree", "--no-commit-id", "--name-status", "-r", "-z", "--root", sha }
            : new[] { "diff-tree", "--no-commit-id", "--name-status", "-r", "-z", firstParent, sha };

        GitCommandResult result = await this.RunAsync(repositoryPath, arguments, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(String.IsNullOrEmpty(result.Error) ? "git diff-tree selhal." : result.Error);
        }

        List<GitFileChange> changes = new();
        string[] records = result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index + 1 < records.Length; index += 2)
        {
            string status = records[index];
            string path = records[index + 1];
            string? originalPath = null;

            if (status.StartsWith('R') || status.StartsWith('C'))
            {
                if (index + 2 < records.Length)
                {
                    originalPath = path;
                    path = records[index + 2];
                    index++;
                }
            }

            changes.Add(new GitFileChange
            {
                Path = path,
                StatusCode = status[..1],
                IsUntracked = false,
                OriginalPath = originalPath
            });

            if (changes.Count >= MaximumFiles)
            {
                break;
            }
        }

        changes.Sort((left, right) => String.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
        return changes;
    }

    public async Task<DiffDocument> GetCommitDiffAsync(
        string repositoryPath,
        string sha,
        string? firstParent,
        GitFileChange file,
        CancellationToken cancellationToken)
    {
        string[] arguments = String.IsNullOrEmpty(firstParent)
            ? new[] { "diff-tree", "--no-commit-id", "-p", "-r", "--root", "--no-color", "-U3", sha, "--", file.Path }
            : new[] { "diff", "--no-color", "-U3", firstParent, sha, "--", file.Path };

        GitCommandResult result = await this.RunAsync(repositoryPath, arguments, cancellationToken);

        return DiffParser.Parse(result.Output);
    }

    public async Task<DiffDocument> GetDiffAsync(string repositoryPath, GitFileChange file, CancellationToken cancellationToken)
    {
        if (file.IsUntracked)
        {
            return this.BuildUntrackedDiff(repositoryPath, file);
        }

        GitCommandResult result = await this.RunAsync(
            repositoryPath,
            new[] { "diff", "HEAD", "--no-color", "-U3", "--", file.Path },
            cancellationToken);

        if (!result.Success || String.IsNullOrEmpty(result.Output))
        {
            GitCommandResult staged = await this.RunAsync(
                repositoryPath,
                new[] { "diff", "--cached", "--no-color", "-U3", "--", file.Path },
                cancellationToken);

            if (staged.Success && !String.IsNullOrEmpty(staged.Output))
            {
                return DiffParser.Parse(staged.Output);
            }
        }

        return DiffParser.Parse(result.Output);
    }

    private DiffDocument BuildUntrackedDiff(string repositoryPath, GitFileChange file)
    {
        string fullPath = Path.Combine(repositoryPath, file.Path.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(fullPath) || !File.Exists(fullPath))
        {
            return DiffDocument.Empty;
        }

        byte[] bytes;
        try
        {
            using FileStream stream = File.OpenRead(fullPath);
            int length = (int)Math.Min(stream.Length, MaximumUntrackedBytes);
            bytes = new byte[length];
            stream.ReadExactly(bytes, 0, length);
        }
        catch (IOException)
        {
            return DiffDocument.Empty;
        }

        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return DiffDocument.Empty;
        }

        string content = Encoding.UTF8.GetString(bytes);
        string[] lines = content.Replace("\r\n", "\n").Split('\n');

        StringBuilder builder = new();
        builder.Append("@@ -0,0 +1,").Append(lines.Length).AppendLine(" @@");

        foreach (string line in lines)
        {
            builder.Append('+').AppendLine(line);
        }

        return DiffParser.Parse(builder.ToString());
    }

    private async Task<GitCommandResult> RunAsync(string workingDirectory, string[] arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";

        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            string output = await outputTask;
            string error = await errorTask;

            return new GitCommandResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new GitCommandResult
            {
                Success = false,
                Output = String.Empty,
                Error = exception.Message
            };
        }
    }

    private static string NormalizeStatus(string status)
    {
        if (status == "??")
        {
            return "U";
        }

        char index = status[0];
        char workTree = status[1];

        if (workTree != ' ' && workTree != '?')
        {
            return workTree.ToString();
        }

        if (index != ' ')
        {
            return index.ToString();
        }

        return "M";
    }
}
