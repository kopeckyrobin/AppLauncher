namespace AppLauncher.Models;

public sealed class LaunchProfile
{
    public required string Name { get; init; }

    public string? ApplicationUrl { get; init; }

    public string? EnvironmentName { get; init; }

    public int EnvironmentVariableCount { get; init; }

    public string PrimaryUrl
    {
        get
        {
            if (String.IsNullOrEmpty(this.ApplicationUrl))
            {
                return String.Empty;
            }

            string[] parts = this.ApplicationUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (part.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }
            }

            return parts.Length > 0 ? parts[0] : String.Empty;
        }
    }

    public override string ToString()
    {
        return this.Name;
    }
}
