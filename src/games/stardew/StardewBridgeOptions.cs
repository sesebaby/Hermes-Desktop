namespace Hermes.Agent.Games.Stardew;

public sealed class StardewBridgeOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 8745;

    public bool AllowLan { get; init; }

    public string? BridgeToken { get; init; }

    public TimeSpan DiscoveryMaxAge { get; init; } = TimeSpan.FromSeconds(30);

    public Uri BaseUri => new($"http://{Host}:{Port}");

    public bool IsLoopbackOnly()
        => !AllowLan &&
           (string.Equals(Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Host, "::1", StringComparison.OrdinalIgnoreCase));
}
