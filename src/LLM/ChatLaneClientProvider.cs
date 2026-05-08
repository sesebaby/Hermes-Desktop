namespace Hermes.Agent.LLM;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public sealed class ChatLaneClientProvider
{
    private readonly ChatClientFactory _clientFactory;
    private readonly ChatRouteResolver _routeResolver;
    private readonly ILogger<ChatLaneClientProvider> _logger;
    private readonly Func<string, string, string?> _readSetting;
    private readonly ConcurrentDictionary<string, IChatClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public ChatLaneClientProvider(
        ChatClientFactory clientFactory,
        ChatRouteResolver routeResolver,
        ILogger<ChatLaneClientProvider> logger,
        Func<string, string, string?> readSetting)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _readSetting = readSetting ?? throw new ArgumentNullException(nameof(readSetting));

        var reservedConcurrency = _readSetting("delegation", "max_concurrent_children");
        var reservedSpawnDepth = _readSetting("delegation", "max_spawn_depth");
        _logger.LogInformation(
            "Delegation lane policy is flat-only in v1; max_spawn_depth={MaxSpawnDepth}; max_spawn_depth_status=reserved_not_enforced; max_concurrent_children={MaxConcurrentChildren}; max_concurrent_children_status=reserved_not_enforced",
            string.IsNullOrWhiteSpace(reservedSpawnDepth) ? "-" : reservedSpawnDepth.Trim(),
            string.IsNullOrWhiteSpace(reservedConcurrency) ? "-" : reservedConcurrency.Trim());
    }

    public IChatClient GetClient(string lane)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lane);
        return _clients.GetOrAdd(lane.Trim(), CreateClient);
    }

    private IChatClient CreateClient(string lane)
    {
        var route = _routeResolver.Resolve(lane);
        _logger.LogInformation(
            "Resolved LLM lane; lane={Lane}; provider={Provider}; model={Model}; baseUrl={BaseUrl}; providerSource={ProviderSource}; modelSource={ModelSource}; baseUrlSource={BaseUrlSource}; apiKeySource={ApiKeySource}",
            route.Lane,
            route.Config.Provider,
            route.Config.Model,
            route.Config.BaseUrl ?? "-",
            route.Sources.Provider,
            route.Sources.Model,
            route.Sources.BaseUrl,
            route.Sources.ApiKey);

        return _clientFactory.CreateClientForConfig(route.Config);
    }
}
