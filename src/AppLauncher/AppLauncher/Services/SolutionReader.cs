using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AppLauncher.Services;

public static partial class SolutionReader
{
    [GeneratedRegex("^Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"(?<path>[^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex SolutionProjectLine();

    public static IReadOnlyList<string> ReadProjectFiles(string solutionFilePath)
    {
        string? solutionDirectory = Path.GetDirectoryName(solutionFilePath);
        if (String.IsNullOrEmpty(solutionDirectory))
        {
            return Array.Empty<string>();
        }

        IEnumerable<string> relativePaths;
        if (Path.GetExtension(solutionFilePath).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            relativePaths = ReadFromSlnx(solutionFilePath);
        }
        else
        {
            relativePaths = ReadFromSln(solutionFilePath);
        }

        List<string> result = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string relativePath in relativePaths)
        {
            if (!relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, normalized));

            if (File.Exists(fullPath) && seen.Add(fullPath))
            {
                result.Add(fullPath);
            }
        }

        return result;
    }

    private static IEnumerable<string> ReadFromSln(string solutionFilePath)
    {
        string content;
        try
        {
            content = File.ReadAllText(solutionFilePath);
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (Match match in SolutionProjectLine().Matches(content))
        {
            yield return match.Groups["path"].Value;
        }
    }

    private static IEnumerable<string> ReadFromSlnx(string solutionFilePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(solutionFilePath);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            yield break;
        }

        foreach (XElement element in document.Descendants("Project"))
        {
            string? path = element.Attribute("Path")?.Value;
            if (!String.IsNullOrEmpty(path))
            {
                yield return path;
            }
        }
    }
}
