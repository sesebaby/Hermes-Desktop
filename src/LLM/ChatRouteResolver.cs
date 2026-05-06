namespace Hermes.Agent.LLM;

public static class ChatRouteNames
{
    public const string Model = "model";
    public const string StardewAutonomy = "stardew_autonomy";
    public const string StardewPrivateChat = "stardew_private_chat";
    public const string Delegation = "delegation";
}

public sealed record ChatRouteResolution(
    string Lane,
    LlmConfig Config,
    ChatRouteFieldSources Sources);

public sealed record ChatRouteFieldSources(
    string Provider,
    string Model,
    string BaseUrl,
    string ApiKey,
    string ApiKeyEnv,
    string AuthMode,
    string AuthHeader,
    string AuthScheme,
    string AuthTokenEnv,
    string AuthTokenCommand,
    string ResponseFormat);

public sealed class ChatRouteResolver
{
    private readonly LlmConfig _rootConfig;
    private readonly Func<string, string, string?> _readSetting;

    public ChatRouteResolver(LlmConfig rootConfig, Func<string, string, string?> readSetting)
    {
        _rootConfig = rootConfig ?? throw new ArgumentNullException(nameof(rootConfig));
        _readSetting = readSetting ?? throw new ArgumentNullException(nameof(readSetting));
    }

    public ChatRouteResolution Resolve(string lane)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lane);

        var laneName = lane.Trim();
        var model = ResolveString(laneName, _rootConfig.Model, "model.default", "model", "default");
        var baseUrl = ResolveNullableString(laneName, _rootConfig.BaseUrl, "model.base_url", "base_url");
        var provider = ResolveProvider(laneName, baseUrl);
        var apiKey = ResolveNullableString(laneName, _rootConfig.ApiKey, "model.api_key", "api_key");
        var apiKeyEnv = ResolveNullableString(laneName, _rootConfig.ApiKeyEnv, "model.api_key_env", "api_key_env");
        var authMode = ResolveNullableString(laneName, _rootConfig.AuthMode, "model.auth_mode", "auth_mode");
        var authHeader = ResolveNullableString(laneName, _rootConfig.AuthHeader, "model.auth_header", "auth_header");
        var authScheme = ResolveNullableString(laneName, _rootConfig.AuthScheme, "model.auth_scheme", "auth_scheme");
        var authTokenEnv = ResolveNullableString(laneName, _rootConfig.AuthTokenEnv, "model.auth_token_env", "auth_token_env");
        var authTokenCommand = ResolveNullableString(laneName, _rootConfig.AuthTokenCommand, "model.auth_token_command", "auth_token_command");
        var responseFormat = ResolveNullableString(laneName, _rootConfig.ResponseFormat, "model.response_format", "response_format");

        return new ChatRouteResolution(
            laneName,
            new LlmConfig
            {
                Provider = provider.Value,
                Model = model.Value,
                BaseUrl = NormalizeBaseUrl(baseUrl.Value),
                ApiKey = apiKey.Value,
                ApiKeyEnv = apiKeyEnv.Value,
                AuthMode = authMode.Value,
                AuthHeader = authHeader.Value,
                AuthScheme = authScheme.Value,
                AuthTokenEnv = authTokenEnv.Value,
                AuthTokenCommand = authTokenCommand.Value,
                ResponseFormat = responseFormat.Value,
                Temperature = _rootConfig.Temperature,
                MaxTokens = _rootConfig.MaxTokens
            },
            new ChatRouteFieldSources(
                provider.Source,
                model.Source,
                baseUrl.Source,
                apiKey.Source,
                apiKeyEnv.Source,
                authMode.Source,
                authHeader.Source,
                authScheme.Source,
                authTokenEnv.Source,
                authTokenCommand.Source,
                responseFormat.Source));
    }

    private (string Value, string Source) ResolveString(
        string lane,
        string rootValue,
        string rootSource,
        params string[] keys)
    {
        var value = ResolveNullableString(lane, rootValue, rootSource, keys);
        return (value.Value ?? rootValue, value.Source);
    }

    private (string Value, string Source) ResolveProvider(
        string lane,
        (string? Value, string Source) baseUrl)
    {
        var provider = ResolveString(lane, _rootConfig.Provider, "model.provider", "provider");
        if (!string.Equals(provider.Source, "model.provider", StringComparison.OrdinalIgnoreCase))
            return provider;
        if (string.Equals(baseUrl.Source, "model.base_url", StringComparison.OrdinalIgnoreCase))
            return provider;

        return ("openai", $"{baseUrl.Source}:openai-compatible");
    }

    private (string? Value, string Source) ResolveNullableString(
        string lane,
        string? rootValue,
        string rootSource,
        params string[] keys)
    {
        if (!string.Equals(lane, ChatRouteNames.Model, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in keys)
            {
                var laneValue = _readSetting(lane, key);
                if (!string.IsNullOrWhiteSpace(laneValue))
                    return (laneValue.Trim(), $"{lane}.{key}");
            }
        }

        return (rootValue, rootSource);
    }

    // Match the desktop root-model behavior for local server bind addresses.
    private static string? NormalizeBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return raw;

        var replacement = uri.HostNameType switch
        {
            UriHostNameType.IPv4 when uri.Host == "0.0.0.0" => "127.0.0.1",
            UriHostNameType.IPv6 when uri.Host == "::" => "::1",
            _ => null
        };
        return replacement is null
            ? raw
            : new UriBuilder(uri) { Host = replacement }.Uri.ToString();
    }
}
