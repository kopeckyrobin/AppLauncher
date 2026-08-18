using AppLauncher.Models;

namespace AppLauncher.Services;

public sealed class RepositoryScanner
{
    private sealed record SolutionGroup(string Name, IReadOnlyList<string> Files);

    public Task<IReadOnlyList<ScannedRepository>> ScanAsync(string rootPath, CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<ScannedRepository>>(() => this.Scan(rootPath, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<ScannedRepository> Scan(string rootPath, CancellationToken cancellationToken)
    {
        List<ScannedRepository> repositories = new();

        if (String.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            return repositories;
        }

        foreach (string repositoryPath in EnumerateDirectories(rootPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourcePath = Path.Combine(repositoryPath, "src");
            if (!Directory.Exists(sourcePath))
            {
                continue;
            }

            List<ScannedSolution> solutions = new();
            HashSet<string> claimedProjects = new(StringComparer.OrdinalIgnoreCase);

            foreach (SolutionGroup group in this.FindSolutionGroups(sourcePath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ScannedProject> projects = new();

                foreach (string solutionFile in group.Files)
                {
                    foreach (string projectFile in SolutionReader.ReadProjectFiles(solutionFile))
                    {
                        if (claimedProjects.Contains(projectFile))
                        {
                            continue;
                        }

                        IReadOnlyList<LaunchProfile> profiles = LaunchSettingsReader.Read(projectFile);
                        if (profiles.Count == 0)
                        {
                            continue;
                        }

                        ProjectMetadata metadata = ProjectFileReader.Read(projectFile);
                        claimedProjects.Add(projectFile);

                        projects.Add(new ScannedProject
                        {
                            Name = Path.GetFileNameWithoutExtension(projectFile),
                            ProjectFilePath = projectFile,
                            Profiles = profiles,
                            TargetFrameworkOverride = metadata.TargetFrameworkOverride,
                            UsesUserSecrets = metadata.UsesUserSecrets
                        });
                    }
                }

                if (projects.Count == 0)
                {
                    continue;
                }

                projects.Sort((left, right) => String.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

                solutions.Add(new ScannedSolution
                {
                    Name = group.Name,
                    SolutionFilePath = group.Files[0],
                    Projects = projects
                });
            }

            if (solutions.Count == 0)
            {
                continue;
            }

            repositories.Add(new ScannedRepository
            {
                Name = Path.GetFileName(repositoryPath),
                DirectoryPath = repositoryPath,
                Solutions = solutions
            });
        }

        return repositories;
    }

    private IReadOnlyList<SolutionGroup> FindSolutionGroups(string sourcePath)
    {
        List<string> candidates = new();

        candidates.AddRange(EnumerateSolutionFiles(sourcePath));

        foreach (string subDirectory in EnumerateDirectories(sourcePath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            candidates.AddRange(EnumerateSolutionFiles(subDirectory));
        }

        Dictionary<string, List<string>> grouped = new(StringComparer.OrdinalIgnoreCase);
        List<string> order = new();

        foreach (string candidate in candidates)
        {
            string key = Path.Combine(
                Path.GetDirectoryName(candidate) ?? String.Empty,
                Path.GetFileNameWithoutExtension(candidate));

            if (!grouped.TryGetValue(key, out List<string>? files))
            {
                files = new List<string>();
                grouped[key] = files;
                order.Add(key);
            }

            files.Add(candidate);
        }

        List<SolutionGroup> result = new();

        foreach (string key in order)
        {
            List<string> files = grouped[key];
            files.Sort((left, right) =>
            {
                bool leftIsModern = Path.GetExtension(left).Equals(".slnx", StringComparison.OrdinalIgnoreCase);
                bool rightIsModern = Path.GetExtension(right).Equals(".slnx", StringComparison.OrdinalIgnoreCase);

                if (leftIsModern == rightIsModern)
                {
                    return String.Compare(left, right, StringComparison.OrdinalIgnoreCase);
                }

                return leftIsModern ? -1 : 1;
            });

            result.Add(new SolutionGroup(Path.GetFileNameWithoutExtension(files[0]), files));
        }

        return result;
    }

    private static IEnumerable<string> EnumerateSolutionFiles(string directory)
    {
        List<string> files = new();

        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.sln*", SearchOption.TopDirectoryOnly))
            {
                string extension = Path.GetExtension(file);
                if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static IEnumerable<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path)
                .Where(directory =>
                {
                    string name = Path.GetFileName(directory);
                    return !name.StartsWith('.') && !name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
