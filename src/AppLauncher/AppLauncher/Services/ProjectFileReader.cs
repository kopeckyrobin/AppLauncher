using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AppLauncher.Services;

public sealed record ProjectMetadata(string? TargetFrameworkOverride, bool UsesUserSecrets);

public static partial class ProjectFileReader
{
    [GeneratedRegex("^net[0-9]+\\.[0-9]+(-[a-z]+[0-9.]*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkToken();

    public static ProjectMetadata Read(string projectFilePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(projectFilePath);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            return new ProjectMetadata(null, false);
        }

        bool usesUserSecrets = document.Descendants()
            .Any(element => element.Name.LocalName == "UserSecretsId" && !String.IsNullOrEmpty(element.Value.Trim()));

        List<string> frameworks = new();

        foreach (XElement element in document.Descendants())
        {
            if (element.Name.LocalName != "TargetFramework" && element.Name.LocalName != "TargetFrameworks")
            {
                continue;
            }

            string[] tokens = element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string token in tokens)
            {
                if (TargetFrameworkToken().IsMatch(token) && !frameworks.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    frameworks.Add(token);
                }
            }
        }

        if (frameworks.Count < 2)
        {
            return new ProjectMetadata(null, usesUserSecrets);
        }

        return new ProjectMetadata(SelectPreferredFramework(frameworks), usesUserSecrets);
    }

    private static string SelectPreferredFramework(List<string> frameworks)
    {
        foreach (string framework in frameworks)
        {
            if (framework.Contains("-windows", StringComparison.OrdinalIgnoreCase))
            {
                return framework;
            }
        }

        foreach (string framework in frameworks)
        {
            if (!framework.Contains('-'))
            {
                return framework;
            }
        }

        return frameworks[0];
    }
}
