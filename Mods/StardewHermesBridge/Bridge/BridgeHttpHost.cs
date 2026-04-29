namespace StardewHermesBridge.Bridge;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewHermesBridge.Logging;

public sealed class BridgeHttpHost
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly BridgeCommandQueue _commands;
    private readonly SmapiBridgeLogger _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public BridgeHttpHost(BridgeCommandQueue commands, SmapiBridgeLogger logger)
    {
        _commands = commands;
        _logger = logger;
    }

    public int Port { get; private set; }

    public string BridgeToken { get; private set; } = "";

    public void Start(string host, int preferredPort)
    {
        if (_listener is not null)
            return;

        BridgeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        Port = preferredPort;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{host}:{Port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunAsync(_cts.Token));
        _logger.Write("bridge_started", null, "bridge", "bridge", null, "online", $"127.0.0.1:{Port}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Close();
        _listener = null;
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch when (ct.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context, ct), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path != "/health" && !IsAuthorized(context.Request))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.Unauthorized, new
                {
                    ok = false,
                    error = new { code = "bridge_unauthorized", message = "Missing or invalid bearer token.", retryable = false }
                }, ct);
                return;
            }

            switch (path)
            {
                case "/health":
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { ok = true, online = true }, ct);
                    return;
                case "/task/move":
                    await HandleMoveAsync(context, ct);
                    return;
                case "/task/status":
                    await HandleStatusAsync(context, ct);
                    return;
                case "/task/cancel":
                    await HandleCancelAsync(context, ct);
                    return;
                case "/action/speak":
                    await HandleSpeakAsync(context, ct);
                    return;
                default:
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { ok = false }, ct);
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.Write("bridge_error", null, "bridge", "bridge", null, "failed", ex.Message);
            await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new { ok = false, error = ex.Message }, ct);
        }
    }

    private async Task HandleMoveAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<MovePayload>>(context.Request, ct);
        var response = _commands.EnqueueMove(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<TaskStatusRequest>>(context.Request, ct);
        var response = _commands.GetStatus(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleCancelAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<TaskCancelRequest>>(context.Request, ct);
        var response = _commands.Cancel(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleSpeakAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<SpeakPayload>>(context.Request, ct);
        var response = _commands.Speak(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        var header = request.Headers["Authorization"];
        return string.Equals(header, $"Bearer {BridgeToken}", StringComparison.Ordinal);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpListenerRequest request, CancellationToken ct)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        ct.ThrowIfCancellationRequested();
        var raw = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Invalid JSON request.");
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode status, object value, CancellationToken ct)
    {
        response.StatusCode = (int)status;
        response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct);
        response.OutputStream.Close();
    }
}
