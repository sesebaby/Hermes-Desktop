using System;
using System.Threading;
using System.Threading.Tasks;

namespace HermesDesktop.Services;

internal enum RuntimeConnectionState
{
    Checking = 0,
    Connected = 1,
    Offline = 2,
    Error = 3,
}

internal sealed record RuntimeStatusSnapshot(
    string Provider,
    string Model,
    string BaseUrl,
    string DisplayProvider,
    string DisplayModel,
    string DisplayBaseUrl,
    RuntimeConnectionState ConnectionState,
    string ConnectionDetail);

/// <summary>
/// Unified source for model/provider/connection runtime status used by desktop pages.
/// </summary>
internal sealed class RuntimeStatusService
{
    private readonly HermesChatService _chatService;

    public RuntimeStatusService(HermesChatService chatService)
    {
        _chatService = chatService;
    }

    public RuntimeStatusSnapshot GetConfiguredSnapshot()
    {
        return CreateSnapshot(RuntimeConnectionState.Checking, string.Empty);
    }

    public async Task<RuntimeStatusSnapshot> RefreshAsync(CancellationToken ct)
    {
        var configured = CreateSnapshot(RuntimeConnectionState.Checking, string.Empty);

        try
        {
            var (isHealthy, detail) = await _chatService.CheckHealthAsync(ct).ConfigureAwait(false);
            return configured with
            {
                ConnectionState = isHealthy ? RuntimeConnectionState.Connected : RuntimeConnectionState.Offline,
                ConnectionDetail = detail ?? string.Empty,
            };
        }
        catch (Exception ex)
        {
            return configured with
            {
                ConnectionState = RuntimeConnectionState.Error,
                ConnectionDetail = ex.Message,
            };
        }
    }

    private static RuntimeStatusSnapshot CreateSnapshot(RuntimeConnectionState state, string detail)
    {
        var provider = NormalizeProvider(HermesEnvironment.ModelProvider);

        return new RuntimeStatusSnapshot(
            Provider: provider,
            Model: HermesEnvironment.DefaultModel,
            BaseUrl: HermesEnvironment.ModelBaseUrl,
            DisplayProvider: NormalizeProvider(HermesEnvironment.DisplayModelProvider),
            DisplayModel: HermesEnvironment.DisplayDefaultModel,
            DisplayBaseUrl: HermesEnvironment.DisplayModelBaseUrl,
            ConnectionState: state,
            ConnectionDetail: detail);
    }

    private static string NormalizeProvider(string provider)
    {
        return string.Equals(provider, "custom", StringComparison.OrdinalIgnoreCase)
            ? "local"
            : provider;
    }
}
