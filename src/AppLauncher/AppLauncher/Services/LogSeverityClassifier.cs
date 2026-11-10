using System.Text.RegularExpressions;
using AppLauncher.Models;

namespace AppLauncher.Services;

public static partial class LogSeverityClassifier
{
    [GeneratedRegex("^(?<level>trce|dbug|info|warn|fail|crit):\\s", RegexOptions.IgnoreCase)]
    private static partial Regex ConsoleLogger();

    [GeneratedRegex("\\[(?:[^\\]\\[]*\\s)?(?<level>VRB|DBG|INF|WRN|ERR|FTL|TRC|VERBOSE|DEBUG|INFORMATION|INFO|WARNING|WARN|ERROR|FATAL|CRITICAL|TRACE)\\]", RegexOptions.IgnoreCase)]
    private static partial Regex BracketedLevel();

    [GeneratedRegex("^\\s*(?:[0-9][0-9:.,/\\-T ]*[Z ]\\s*)?(?<level>TRACE|DEBUG|INFO|WARNING|WARN|ERROR|FATAL|CRITICAL)\\b[:\\s\\]]", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingLevel();

    [GeneratedRegex(":\\s(?<level>error|warning)\\s[A-Za-z]+[0-9]+\\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex BuildDiagnostic();

    [GeneratedRegex("^(?:Build\\s|Time Elapsed|Determining projects|Restored\\s|\\s*[0-9]+\\s+(?:Warning|Error)\\(s\\))")]
    private static partial Regex BuildSummary();

    public static LogSeverity Classify(string line, LogSeverity previous)
    {
        Match match = ConsoleLogger().Match(line);
        if (match.Success)
        {
            return Map(match.Groups["level"].Value);
        }

        match = BuildDiagnostic().Match(line);
        if (match.Success)
        {
            return Map(match.Groups["level"].Value);
        }

        if (BuildSummary().IsMatch(line))
        {
            return LogSeverity.Info;
        }

        match = BracketedLevel().Match(line);
        if (match.Success)
        {
            return Map(match.Groups["level"].Value);
        }

        match = LeadingLevel().Match(line);
        if (match.Success)
        {
            return Map(match.Groups["level"].Value);
        }

        return previous;
    }

    private static LogSeverity Map(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "trce":
            case "trc":
            case "trace":
            case "vrb":
            case "verbose":
                return LogSeverity.Trace;
            case "dbug":
            case "dbg":
            case "debug":
                return LogSeverity.Debug;
            case "warn":
            case "wrn":
            case "warning":
                return LogSeverity.Warning;
            case "fail":
            case "err":
            case "error":
            case "crit":
            case "ftl":
            case "fatal":
            case "critical":
                return LogSeverity.Error;
            default:
                return LogSeverity.Info;
        }
    }
}
