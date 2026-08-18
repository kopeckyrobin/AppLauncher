using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace AppLauncher.Services;

public sealed class UpdateService
{
    private const string FeedUrlMetadataKey = "UpdateFeedUrl";

    private readonly UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;

    public UpdateService()
    {
        string feedUrl = ReadFeedUrl();

        if (String.IsNullOrEmpty(feedUrl))
        {
            return;
        }

        try
        {
            this._manager = new UpdateManager(new GithubSource(feedUrl, null, false));
        }
        catch (Exception)
        {
            this._manager = null;
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (this._manager is null)
            {
                return false;
            }

            return this._manager.IsInstalled;
        }
    }

    public string CurrentVersion
    {
        get
        {
            SemanticVersion? installed = this._manager?.CurrentVersion;

            if (installed is not null)
            {
                return installed.ToString();
            }

            return ReadAssemblyVersion();
        }
    }

    public string PendingVersion
    {
        get
        {
            if (this._pendingUpdate is null)
            {
                return String.Empty;
            }

            return this._pendingUpdate.TargetFullRelease.Version.ToString();
        }
    }

    public async Task<bool> CheckAsync()
    {
        if (!this.IsEnabled)
        {
            return false;
        }

        this._pendingUpdate = await this._manager!.CheckForUpdatesAsync();

        return this._pendingUpdate is not null;
    }

    public async Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken)
    {
        if (this._manager is null || this._pendingUpdate is null)
        {
            return;
        }

        await this._manager.DownloadUpdatesAsync(this._pendingUpdate, progress, cancellationToken);
    }

    public void ApplyAndRestart()
    {
        if (this._manager is null || this._pendingUpdate is null)
        {
            return;
        }

        this._manager.ApplyUpdatesAndRestart(this._pendingUpdate.TargetFullRelease);
    }

    private static string ReadFeedUrl()
    {
        foreach (AssemblyMetadataAttribute attribute in typeof(UpdateService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (String.Equals(attribute.Key, FeedUrlMetadataKey, StringComparison.Ordinal))
            {
                return attribute.Value ?? String.Empty;
            }
        }

        return String.Empty;
    }

    private static string ReadAssemblyVersion()
    {
        AssemblyInformationalVersionAttribute? attribute = typeof(UpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute is null || String.IsNullOrEmpty(attribute.InformationalVersion))
        {
            return "0.0.0";
        }

        int metadataSeparator = attribute.InformationalVersion.IndexOf('+');

        if (metadataSeparator < 0)
        {
            return attribute.InformationalVersion;
        }

        return attribute.InformationalVersion.Substring(0, metadataSeparator);
    }
}
