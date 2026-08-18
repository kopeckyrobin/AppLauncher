namespace AppLauncher.Models;

public sealed class ScannedProject
{
    public required string Name { get; init; }

    public required string ProjectFilePath { get; init; }

    public required IReadOnlyList<LaunchProfile> Profiles { get; init; }

    public string? TargetFrameworkOverride { get; init; }

    public bool UsesUserSecrets { get; init; }
}

public sealed class ScannedSolution
{
    public required string Name { get; init; }

    public required string SolutionFilePath { get; init; }

    public required IReadOnlyList<ScannedProject> Projects { get; init; }
}

public sealed class ScannedRepository
{
    public required string Name { get; init; }

    public required string DirectoryPath { get; init; }

    public required IReadOnlyList<ScannedSolution> Solutions { get; init; }
}
