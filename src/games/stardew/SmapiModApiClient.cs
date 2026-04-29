namespace Hermes.Agent.Games.Stardew;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public interface ISmapiModApiClient
{
    Task<StardewBridgeResponse<TData>> SendAsync<TPayload, TData>(
        string route,
        StardewBridgeEnvelope<TPayload> envelope,
        CancellationToken ct);
}

public sealed class SmapiModApiClient : ISmapiModApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly StardewBridgeOptions _options;

    public SmapiModApiClient(HttpClient httpClient, StardewBridgeOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<StardewBridgeResponse<TData>> SendAsync<TPayload, TData>(
        string route,
        StardewBridgeEnvelope<TPayload> envelope,
        CancellationToken ct)
    {
        if (!_options.IsLoopbackOnly())
            throw new InvalidOperationException(StardewBridgeErrorCodes.BridgeStaleDiscovery);

        if (!string.Equals(route, StardewBridgeRoutes.Health, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(_options.BridgeToken))
        {
            throw new InvalidOperationException(StardewBridgeErrorCodes.BridgeUnauthorized);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseUri, route.TrimStart('/')));
        if (!string.IsNullOrWhiteSpace(_options.BridgeToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BridgeToken);

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<StardewBridgeResponse<TData>>(body, JsonOptions);
        if (result is null)
            throw new InvalidOperationException("SMAPI bridge returned an unreadable response.");

        return result;
    }
}
