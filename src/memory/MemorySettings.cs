namespace Hermes.Agent.Memory;

/// <summary>
/// Shared built-in memory feature defaults.
/// </summary>
public static class MemorySettings
{
    public const bool DefaultEnabled = true;

    public static bool IsEnabled(string? rawValue, bool defaultValue = DefaultEnabled)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return defaultValue;

        return bool.TryParse(rawValue.Trim(), out var parsed)
            ? parsed
            : defaultValue;
    }
}
