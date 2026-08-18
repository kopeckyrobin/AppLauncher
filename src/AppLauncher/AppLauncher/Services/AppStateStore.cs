using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppLauncher.Services;

public sealed class AppState
{
    [JsonPropertyName("repositoriesRoot")]
    public string? RepositoriesRoot { get; set; }

    [JsonPropertyName("lastProfiles")]
    public Dictionary<string, string> LastProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("collapsedRepositories")]
    public List<string> CollapsedRepositories { get; set; } = new();
}

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _gate = new();
    private AppState _state = new();

    public AppStateStore()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppLauncher");

        Directory.CreateDirectory(directory);
        this._filePath = Path.Combine(directory, "state.json");
    }

    public string RepositoriesRoot
    {
        get
        {
            if (!String.IsNullOrEmpty(this._state.RepositoriesRoot))
            {
                return this._state.RepositoriesRoot;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "source",
                "repos");
        }
        set
        {
            this._state.RepositoriesRoot = value;
            this.Save();
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(this._filePath))
            {
                return;
            }

            AppState? loaded = JsonSerializer.Deserialize<AppState>(File.ReadAllText(this._filePath));
            if (loaded is not null)
            {
                loaded.LastProfiles = new Dictionary<string, string>(loaded.LastProfiles, StringComparer.OrdinalIgnoreCase);
                this._state = loaded;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            this._state = new AppState();
        }
    }

    public string? GetLastProfile(string projectFilePath)
    {
        if (this._state.LastProfiles.TryGetValue(projectFilePath, out string? profileName))
        {
            return profileName;
        }

        return null;
    }

    public void SetLastProfile(string projectFilePath, string profileName)
    {
        if (this._state.LastProfiles.TryGetValue(projectFilePath, out string? existing) && existing == profileName)
        {
            return;
        }

        this._state.LastProfiles[projectFilePath] = profileName;
        this.Save();
    }

    public bool IsCollapsed(string repositoryPath)
    {
        return this._state.CollapsedRepositories.Contains(repositoryPath, StringComparer.OrdinalIgnoreCase);
    }

    public void SetCollapsed(string repositoryPath, bool isCollapsed)
    {
        bool changed;

        if (isCollapsed)
        {
            changed = !this._state.CollapsedRepositories.Contains(repositoryPath, StringComparer.OrdinalIgnoreCase);
            if (changed)
            {
                this._state.CollapsedRepositories.Add(repositoryPath);
            }
        }
        else
        {
            changed = this._state.CollapsedRepositories.RemoveAll(
                item => String.Equals(item, repositoryPath, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        if (changed)
        {
            this.Save();
        }
    }

    private void Save()
    {
        lock (this._gate)
        {
            try
            {
                File.WriteAllText(this._filePath, JsonSerializer.Serialize(this._state, SerializerOptions));
            }
            catch (IOException)
            {
            }
        }
    }
}
