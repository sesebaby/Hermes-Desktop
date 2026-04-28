namespace Hermes.Agent.Skills;

using System.Text.RegularExpressions;

internal static class FuzzyTextReplacer
{
    public static FuzzyReplaceResult Replace(
        string content,
        string oldString,
        string newString,
        bool replaceAll)
    {
        if (string.IsNullOrEmpty(oldString))
            return FuzzyReplaceResult.Fail("old_string cannot be empty");
        if (oldString == newString)
            return FuzzyReplaceResult.Fail("old_string and new_string are identical");

        var strategies = new (string Name, Func<string, string, IReadOnlyList<TextRange>> Match)[]
        {
            ("exact", ExactMatches),
            ("line_trimmed", LineTrimmedMatches),
            ("whitespace_normalized", WhitespaceNormalizedMatches),
            ("indentation_flexible", IndentationFlexibleMatches),
            ("escape_normalized", EscapeNormalizedMatches),
            ("trimmed_boundary", TrimmedBoundaryMatches),
            ("unicode_normalized", UnicodeNormalizedMatches),
            ("block_anchor", BlockAnchorMatches),
            ("context_aware", ContextAwareMatches)
        };

        foreach (var (name, matcher) in strategies)
        {
            var matches = matcher(content, oldString);
            if (matches.Count == 0)
                continue;

            if (matches.Count > 1 && !replaceAll)
            {
                return FuzzyReplaceResult.Fail(
                    $"Found {matches.Count} matches for old_string. Provide more context to make it unique, or use replace_all=true.");
            }

            var updated = Apply(content, matches, newString);
            return FuzzyReplaceResult.Ok(updated, matches.Count, name);
        }

        return FuzzyReplaceResult.Fail("Could not find a match for old_string in the file");
    }

    public static string FormatNoMatchHint(string error, string oldString, string content)
    {
        if (!error.StartsWith("Could not find", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(oldString) ||
            string.IsNullOrWhiteSpace(content))
        {
            return "";
        }

        var oldLines = SplitLines(oldString);
        var contentLines = SplitLines(content);
        var anchor = oldLines.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        if (anchor is null)
            return "";

        var scored = contentLines
            .Select((line, index) => (Score: Similarity(anchor, line.Trim()), Index: index))
            .Where(item => item.Score > 0.3)
            .OrderByDescending(item => item.Score)
            .Take(3)
            .ToList();
        if (scored.Count == 0)
            return "";

        var snippets = new List<string>();
        var seen = new HashSet<string>();
        foreach (var (_, index) in scored)
        {
            var start = Math.Max(0, index - 2);
            var end = Math.Min(contentLines.Length, index + oldLines.Length + 2);
            var key = $"{start}:{end}";
            if (!seen.Add(key))
                continue;

            snippets.Add(string.Join("\n", Enumerable.Range(start, end - start)
                .Select(i => $"{i + 1,4}| {contentLines[i]}")));
        }

        return snippets.Count == 0
            ? ""
            : "\n\nDid you mean one of these sections?\n" + string.Join("\n---\n", snippets);
    }

    private static IReadOnlyList<TextRange> ExactMatches(string content, string pattern)
    {
        var matches = new List<TextRange>();
        var index = 0;
        while ((index = content.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            matches.Add(new TextRange(index, index + pattern.Length));
            index++;
        }

        return matches;
    }

    private static IReadOnlyList<TextRange> LineTrimmedMatches(string content, string pattern)
    {
        var contentLines = SplitLines(content);
        var normalized = contentLines.Select(line => line.Trim()).ToArray();
        var patternNormalized = string.Join('\n', SplitLines(pattern).Select(line => line.Trim()));
        return FindNormalizedLineMatches(content, contentLines, normalized, patternNormalized);
    }

    private static IReadOnlyList<TextRange> WhitespaceNormalizedMatches(string content, string pattern)
    {
        static string Normalize(string value) => Regex.Replace(value, "[ \\t]+", " ");
        var normalizedContent = Normalize(content);
        var normalizedPattern = Normalize(pattern);
        var normalizedMatches = ExactMatches(normalizedContent, normalizedPattern);
        return MapNormalizedPositions(content, normalizedContent, normalizedMatches);
    }

    private static IReadOnlyList<TextRange> IndentationFlexibleMatches(string content, string pattern)
    {
        var contentLines = SplitLines(content);
        var normalized = contentLines.Select(line => line.TrimStart()).ToArray();
        var patternNormalized = string.Join('\n', SplitLines(pattern).Select(line => line.TrimStart()));
        return FindNormalizedLineMatches(content, contentLines, normalized, patternNormalized);
    }

    private static IReadOnlyList<TextRange> EscapeNormalizedMatches(string content, string pattern)
    {
        var unescaped = pattern
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal);
        return unescaped == pattern ? Array.Empty<TextRange>() : ExactMatches(content, unescaped);
    }

    private static IReadOnlyList<TextRange> TrimmedBoundaryMatches(string content, string pattern)
    {
        var patternLines = SplitLines(pattern);
        if (patternLines.Length == 0)
            return Array.Empty<TextRange>();

        patternLines[0] = patternLines[0].Trim();
        if (patternLines.Length > 1)
            patternLines[^1] = patternLines[^1].Trim();

        var contentLines = SplitLines(content);
        var matches = new List<TextRange>();
        for (var i = 0; i <= contentLines.Length - patternLines.Length; i++)
        {
            var block = contentLines.Skip(i).Take(patternLines.Length).ToArray();
            block[0] = block[0].Trim();
            if (block.Length > 1)
                block[^1] = block[^1].Trim();

            if (string.Join('\n', block) == string.Join('\n', patternLines))
                matches.Add(CalculateLineRange(contentLines, i, i + patternLines.Length, content.Length));
        }

        return matches;
    }

    private static IReadOnlyList<TextRange> UnicodeNormalizedMatches(string content, string pattern)
    {
        var normalizedContent = NormalizeUnicode(content);
        var normalizedPattern = NormalizeUnicode(pattern);
        if (normalizedContent == content && normalizedPattern == pattern)
            return Array.Empty<TextRange>();

        var normalizedMatches = ExactMatches(normalizedContent, normalizedPattern);
        return MapNormalizedPositions(content, normalizedContent, normalizedMatches);
    }

    private static IReadOnlyList<TextRange> BlockAnchorMatches(string content, string pattern)
    {
        var patternLines = SplitLines(NormalizeUnicode(pattern));
        if (patternLines.Length < 2)
            return Array.Empty<TextRange>();

        var first = patternLines[0].Trim();
        var last = patternLines[^1].Trim();
        var normalizedContentLines = SplitLines(NormalizeUnicode(content));
        var originalContentLines = SplitLines(content);
        var matches = new List<TextRange>();

        for (var i = 0; i <= normalizedContentLines.Length - patternLines.Length; i++)
        {
            if (normalizedContentLines[i].Trim() != first ||
                normalizedContentLines[i + patternLines.Length - 1].Trim() != last)
            {
                continue;
            }

            var similarity = 1.0;
            if (patternLines.Length > 2)
            {
                var contentMiddle = string.Join('\n', normalizedContentLines.Skip(i + 1).Take(patternLines.Length - 2));
                var patternMiddle = string.Join('\n', patternLines.Skip(1).Take(patternLines.Length - 2));
                similarity = Similarity(patternMiddle, contentMiddle);
            }

            if (similarity >= 0.5)
                matches.Add(CalculateLineRange(originalContentLines, i, i + patternLines.Length, content.Length));
        }

        return matches;
    }

    private static IReadOnlyList<TextRange> ContextAwareMatches(string content, string pattern)
    {
        var patternLines = SplitLines(pattern);
        var contentLines = SplitLines(content);
        if (patternLines.Length == 0)
            return Array.Empty<TextRange>();

        var matches = new List<TextRange>();
        for (var i = 0; i <= contentLines.Length - patternLines.Length; i++)
        {
            var highSimilarity = 0;
            for (var j = 0; j < patternLines.Length; j++)
            {
                if (Similarity(patternLines[j].Trim(), contentLines[i + j].Trim()) >= 0.80)
                    highSimilarity++;
            }

            if (highSimilarity >= patternLines.Length * 0.5)
                matches.Add(CalculateLineRange(contentLines, i, i + patternLines.Length, content.Length));
        }

        return matches;
    }

    private static IReadOnlyList<TextRange> FindNormalizedLineMatches(
        string content,
        string[] contentLines,
        string[] normalizedContentLines,
        string normalizedPattern)
    {
        var patternLines = SplitLines(normalizedPattern);
        var matches = new List<TextRange>();
        for (var i = 0; i <= normalizedContentLines.Length - patternLines.Length; i++)
        {
            var block = string.Join('\n', normalizedContentLines.Skip(i).Take(patternLines.Length));
            if (block == normalizedPattern)
                matches.Add(CalculateLineRange(contentLines, i, i + patternLines.Length, content.Length));
        }

        return matches;
    }

    private static IReadOnlyList<TextRange> MapNormalizedPositions(
        string original,
        string normalized,
        IReadOnlyList<TextRange> normalizedMatches)
    {
        if (normalizedMatches.Count == 0)
            return Array.Empty<TextRange>();

        var matches = new List<TextRange>();
        foreach (var match in normalizedMatches)
        {
            var start = MapNormalizedIndexToOriginal(original, normalized, match.Start);
            var end = MapNormalizedIndexToOriginal(original, normalized, match.End);
            matches.Add(new TextRange(start, Math.Min(end, original.Length)));
        }

        return matches;
    }

    private static int MapNormalizedIndexToOriginal(string original, string normalized, int normalizedIndex)
    {
        var oi = 0;
        var ni = 0;
        while (oi < original.Length && ni < normalizedIndex)
        {
            var normalizedChar = NormalizeUnicode(original[oi].ToString());
            ni += normalizedChar.Length;
            oi++;
            while (oi < original.Length &&
                   original[oi] is ' ' or '\t' &&
                   ni < normalized.Length &&
                   normalized[Math.Min(ni, normalized.Length - 1)] == ' ')
            {
                oi++;
            }
        }

        return oi;
    }

    private static TextRange CalculateLineRange(string[] lines, int startLine, int endLine, int contentLength)
    {
        var start = lines.Take(startLine).Sum(line => line.Length + 1);
        var end = lines.Take(endLine).Sum(line => line.Length + 1) - 1;
        return new TextRange(start, Math.Min(Math.Max(end, start), contentLength));
    }

    private static string Apply(string content, IReadOnlyList<TextRange> matches, string newString)
    {
        var result = content;
        foreach (var match in matches.OrderByDescending(match => match.Start))
            result = result[..match.Start] + newString + result[match.End..];
        return result;
    }

    private static string[] SplitLines(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string NormalizeUnicode(string value)
        => value
            .Replace("\u201c", "\"", StringComparison.Ordinal)
            .Replace("\u201d", "\"", StringComparison.Ordinal)
            .Replace("\u2018", "'", StringComparison.Ordinal)
            .Replace("\u2019", "'", StringComparison.Ordinal)
            .Replace("\u2014", "--", StringComparison.Ordinal)
            .Replace("\u2013", "-", StringComparison.Ordinal)
            .Replace("\u2026", "...", StringComparison.Ordinal)
            .Replace("\u00a0", " ", StringComparison.Ordinal);

    private static double Similarity(string left, string right)
    {
        if (left == right)
            return 1.0;
        if (left.Length == 0 || right.Length == 0)
            return 0.0;

        var distance = LevenshteinDistance(left, right);
        return 1.0 - distance / (double)Math.Max(left.Length, right.Length);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private readonly record struct TextRange(int Start, int End);
}

internal sealed record FuzzyReplaceResult(
    bool Success,
    string Content,
    int MatchCount,
    string? Strategy,
    string? Error)
{
    public static FuzzyReplaceResult Ok(string content, int matchCount, string strategy)
        => new(true, content, matchCount, strategy, null);

    public static FuzzyReplaceResult Fail(string error)
        => new(false, "", 0, null, error);
}
