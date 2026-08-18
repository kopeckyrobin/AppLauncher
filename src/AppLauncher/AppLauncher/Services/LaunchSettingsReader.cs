using System.Text.Json;
using AppLauncher.Models;

namespace AppLauncher.Services;

public static class LaunchSettingsReader
{
    private static readonly string[] EnvironmentKeys =
    {
        "ASPNETCORE_ENVIRONMENT",
        "DOTNET_ENVIRONMENT"
    };

    public static IReadOnlyList<LaunchProfile> Read(string projectFilePath)
    {
        string? projectDirectory = Path.GetDirectoryName(projectFilePath);
        if (String.IsNullOrEmpty(projectDirectory))
        {
            return Array.Empty<LaunchProfile>();
        }

        string settingsPath = Path.Combine(projectDirectory, "Properties", "launchSettings.json");
        if (!File.Exists(settingsPath))
        {
            return Array.Empty<LaunchProfile>();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(settingsPath), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return Array.Empty<LaunchProfile>();
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("profiles", out JsonElement profiles) ||
                profiles.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<LaunchProfile>();
            }

            List<LaunchProfile> result = new();

            foreach (JsonProperty profile in profiles.EnumerateObject())
            {
                if (profile.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!IsRunnable(profile.Value))
                {
                    continue;
                }

                result.Add(new LaunchProfile
                {
                    Name = profile.Name,
                    ApplicationUrl = ReadString(profile.Value, "applicationUrl"),
                    EnvironmentName = ReadEnvironmentName(profile.Value),
                    EnvironmentVariableCount = CountEnvironmentVariables(profile.Value)
                });
            }

            return result;
        }
    }

    private static bool IsRunnable(JsonElement profile)
    {
        if (!profile.TryGetProperty("commandName", out JsonElement commandName))
        {
            return true;
        }

        return commandName.ValueKind == JsonValueKind.String &&
               String.Equals(commandName.GetString(), "Project", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string? ReadEnvironmentName(JsonElement profile)
    {
        if (!profile.TryGetProperty("environmentVariables", out JsonElement variables) ||
            variables.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (string key in EnvironmentKeys)
        {
            if (variables.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int CountEnvironmentVariables(JsonElement profile)
    {
        if (!profile.TryGetProperty("environmentVariables", out JsonElement variables) ||
            variables.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        int count = 0;
        foreach (JsonProperty unused in variables.EnumerateObject())
        {
            count++;
        }

        return count;
    }
}
