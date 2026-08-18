namespace AppLauncher.ViewModels;

public sealed class SolutionViewModel
{
    public required string Name { get; init; }

    public required string SolutionFilePath { get; init; }

    public required IReadOnlyList<ProjectViewModel> Projects { get; init; }
}
