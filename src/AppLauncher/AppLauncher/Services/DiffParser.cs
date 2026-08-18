using System.Text.RegularExpressions;
using AppLauncher.Models;

namespace AppLauncher.Services;

public static partial class DiffParser
{
    private const int MaximumLines = 4000;

    [GeneratedRegex("^@@ -(?<oldStart>[0-9]+)(,(?<oldCount>[0-9]+))? \\+(?<newStart>[0-9]+)(,(?<newCount>[0-9]+))? @@")]
    private static partial Regex HunkHeader();

    public static DiffDocument Parse(string diffText)
    {
        if (String.IsNullOrEmpty(diffText))
        {
            return DiffDocument.Empty;
        }

        List<DiffLine> inlineLines = new();
        List<DiffRow> sideRows = new();
        List<string> pendingRemoved = new();
        List<string> pendingRemovedNumbers = new();
        List<string> pendingAdded = new();
        List<string> pendingAddedNumbers = new();

        int addedCount = 0;
        int removedCount = 0;
        int oldLine = 0;
        int newLine = 0;
        bool insideHunk = false;
        bool truncated = false;

        string[] rawLines = diffText.Replace("\r\n", "\n").Split('\n');

        foreach (string rawLine in rawLines)
        {
            if (inlineLines.Count >= MaximumLines)
            {
                truncated = true;
                break;
            }

            Match hunkMatch = HunkHeader().Match(rawLine);
            if (hunkMatch.Success)
            {
                FlushPending(sideRows, pendingRemoved, pendingRemovedNumbers, pendingAdded, pendingAddedNumbers);

                oldLine = int.Parse(hunkMatch.Groups["oldStart"].Value);
                newLine = int.Parse(hunkMatch.Groups["newStart"].Value);
                insideHunk = true;

                inlineLines.Add(new DiffLine { Kind = DiffLineKind.Hunk, Text = rawLine });
                sideRows.Add(new DiffRow
                {
                    LeftKind = DiffLineKind.Hunk,
                    RightKind = DiffLineKind.Hunk,
                    LeftText = rawLine
                });
                continue;
            }

            if (!insideHunk)
            {
                continue;
            }

            if (rawLine.StartsWith('\\'))
            {
                continue;
            }

            if (rawLine.StartsWith('+'))
            {
                addedCount++;
                string text = rawLine[1..];
                string number = newLine.ToString();
                newLine++;

                inlineLines.Add(new DiffLine
                {
                    Kind = DiffLineKind.Added,
                    Text = text,
                    NewNumber = number
                });

                pendingAdded.Add(text);
                pendingAddedNumbers.Add(number);
                continue;
            }

            if (rawLine.StartsWith('-'))
            {
                removedCount++;
                string text = rawLine[1..];
                string number = oldLine.ToString();
                oldLine++;

                inlineLines.Add(new DiffLine
                {
                    Kind = DiffLineKind.Removed,
                    Text = text,
                    OldNumber = number
                });

                pendingRemoved.Add(text);
                pendingRemovedNumbers.Add(number);
                continue;
            }

            if (rawLine.Length == 0 || rawLine.StartsWith(' '))
            {
                FlushPending(sideRows, pendingRemoved, pendingRemovedNumbers, pendingAdded, pendingAddedNumbers);

                string text = rawLine.Length == 0 ? String.Empty : rawLine[1..];
                string oldNumber = oldLine.ToString();
                string newNumber = newLine.ToString();
                oldLine++;
                newLine++;

                inlineLines.Add(new DiffLine
                {
                    Kind = DiffLineKind.Context,
                    Text = text,
                    OldNumber = oldNumber,
                    NewNumber = newNumber
                });

                sideRows.Add(new DiffRow
                {
                    LeftKind = DiffLineKind.Context,
                    RightKind = DiffLineKind.Context,
                    LeftNumber = oldNumber,
                    LeftText = text,
                    RightNumber = newNumber,
                    RightText = text
                });
            }
        }

        FlushPending(sideRows, pendingRemoved, pendingRemovedNumbers, pendingAdded, pendingAddedNumbers);

        return new DiffDocument
        {
            InlineLines = inlineLines,
            SideRows = sideRows,
            AddedCount = addedCount,
            RemovedCount = removedCount,
            Markers = BuildMarkers(inlineLines),
            IsTruncated = truncated
        };
    }

    private static void FlushPending(
        List<DiffRow> sideRows,
        List<string> removed,
        List<string> removedNumbers,
        List<string> added,
        List<string> addedNumbers)
    {
        int count = Math.Max(removed.Count, added.Count);

        for (int index = 0; index < count; index++)
        {
            bool hasRemoved = index < removed.Count;
            bool hasAdded = index < added.Count;

            sideRows.Add(new DiffRow
            {
                LeftKind = hasRemoved ? DiffLineKind.Removed : DiffLineKind.Filler,
                RightKind = hasAdded ? DiffLineKind.Added : DiffLineKind.Filler,
                LeftNumber = hasRemoved ? removedNumbers[index] : String.Empty,
                LeftText = hasRemoved ? removed[index] : String.Empty,
                RightNumber = hasAdded ? addedNumbers[index] : String.Empty,
                RightText = hasAdded ? added[index] : String.Empty
            });
        }

        removed.Clear();
        removedNumbers.Clear();
        added.Clear();
        addedNumbers.Clear();
    }

    private static IReadOnlyList<ChangeMarker> BuildMarkers(List<DiffLine> lines)
    {
        List<ChangeMarker> markers = new();

        if (lines.Count == 0)
        {
            return markers;
        }

        int index = 0;
        double total = lines.Count;

        while (index < lines.Count)
        {
            DiffLineKind kind = lines[index].Kind;

            if (kind != DiffLineKind.Added && kind != DiffLineKind.Removed)
            {
                index++;
                continue;
            }

            int start = index;
            bool hasAddition = false;

            while (index < lines.Count &&
                   (lines[index].Kind == DiffLineKind.Added || lines[index].Kind == DiffLineKind.Removed))
            {
                if (lines[index].Kind == DiffLineKind.Added)
                {
                    hasAddition = true;
                }

                index++;
            }

            markers.Add(new ChangeMarker
            {
                Start = start / total,
                End = index / total,
                IsAddition = hasAddition
            });
        }

        return markers;
    }
}
