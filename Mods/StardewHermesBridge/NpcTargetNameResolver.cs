namespace StardewHermesBridge;

public static class NpcTargetNameResolver
{
    public static bool TryResolveKnownAlias(string input, out string? npcName)
    {
        npcName = input.Trim() switch
        {
            "Haley" or "haley" or "海莉" => "Haley",
            "Penny" or "penny" or "潘妮" => "Penny",
            _ => null
        };

        return npcName is not null;
    }

    internal static IEnumerable<string> EnumerateCandidates(string? rawNpcId)
    {
        if (string.IsNullOrWhiteSpace(rawNpcId))
            yield break;

        var trimmed = rawNpcId.Trim();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in BuildCandidates(trimmed))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<string?> BuildCandidates(string trimmed)
    {
        yield return trimmed;

        if (TryResolveKnownAlias(trimmed, out var knownNpcName))
            yield return knownNpcName;

        if (trimmed.Length > 1)
            yield return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
        else
            yield return trimmed.ToUpperInvariant();
    }
}
