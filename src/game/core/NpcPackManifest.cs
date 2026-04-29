namespace Hermes.Agent.Game;

using System.Text.Json.Serialization;

public sealed class NpcPackManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("npcId")]
    public string NpcId { get; init; } = "";

    [JsonPropertyName("gameId")]
    public string GameId { get; init; } = "";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; init; } = "";

    [JsonPropertyName("defaultProfileId")]
    public string DefaultProfileId { get; init; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("smapiName")]
    public string SmapiName { get; init; } = "";

    [JsonPropertyName("aliases")]
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    [JsonPropertyName("targetEntityId")]
    public string TargetEntityId { get; init; } = "";

    [JsonPropertyName("adapterId")]
    public string AdapterId { get; init; } = "";

    [JsonPropertyName("soulFile")]
    public string SoulFile { get; init; } = "";

    [JsonPropertyName("factsFile")]
    public string FactsFile { get; init; } = "";

    [JsonPropertyName("voiceFile")]
    public string VoiceFile { get; init; } = "";

    [JsonPropertyName("boundariesFile")]
    public string BoundariesFile { get; init; } = "";

    [JsonPropertyName("skillsFile")]
    public string SkillsFile { get; init; } = "";

    [JsonPropertyName("policies")]
    public IReadOnlyDictionary<string, string> Policies { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}

public sealed record NpcPack(NpcPackManifest Manifest, string RootPath);

public sealed record NpcPackValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static NpcPackValidationResult Valid { get; } = new(true, Array.Empty<string>());

    public static NpcPackValidationResult Invalid(IEnumerable<string> errors)
        => new(false, errors.ToArray());
}
