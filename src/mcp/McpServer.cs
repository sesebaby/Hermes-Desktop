namespace Hermes.Agent.Mcp;

using System.Net;
using System.Text;
using System.Text.Json;
using Hermes.Agent.Core;
using Microsoft.Extensions.Logging;

// ══════════════════════════════════════════════
// MCP Server Mode
// ══════════════════════════════════════════════
//
// Upstream ref: mcp_serve.py
// Exposes Hermes tools via MCP protocol so other agents/tools
// can call Hermes as a service.
// Uses HTTP/SSE transport (most compatible).

/// <summary>
/// Exposes registered Hermes tools as an MCP server.
/// Other MCP clients can discover and invoke tools via HTTP.
/// </summary>
public sealed class McpServer : IAsyncDisposable
{
    private const string PreferredProtocolVersion = "2025-11-25";
    private const string CompatibilityProtocolVersion = "2025-06-18";
    private const string ServerName = "hermes-desktop";
    private const string ServerVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { }, JsonOptions);
    private static readonly HashSet<string> SupportedProtocolVersions = new(StringComparer.Ordinal)
    {
        PreferredProtocolVersion,
        CompatibilityProtocolVersion
    };

    private readonly ILogger<McpServer> _logger;
    private readonly Dictionary<string, ITool> _tools;
    private readonly string? _authToken;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _running;

    /// <param name="authToken">Bearer token for authentication. If set, all requests must include it. Auto-generated if null.</param>
    public McpServer(IReadOnlyDictionary<string, ITool> tools, ILogger<McpServer> logger, string? authToken = null)
    {
        _tools = new Dictionary<string, ITool>(tools);
        _logger = logger;
        _authToken = authToken ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>Auth token clients must send as Bearer token.</summary>
    public string AuthToken => _authToken!;

    public bool IsRunning => _running;
    public int Port { get; private set; }

    /// <summary>Start the MCP server on the specified port.</summary>
    public async Task StartAsync(int port = 3100, CancellationToken ct = default)
    {
        Port = port;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        _running = true;
        _logger.LogInformation("MCP server started on port {Port} with {Count} tools (token: {Token})",
            port, _tools.Count, _authToken![..8] + "...");

        _ = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (_running && !ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener!.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "MCP server request error");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var method = context.Request.HttpMethod;

        var authHeader = context.Request.Headers["Authorization"];
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.Equals($"Bearer {_authToken}", StringComparison.Ordinal))
        {
            await WritePlainJsonAsync(
                context.Response,
                401,
                new { error = "Unauthorized - include Authorization: Bearer <token>" },
                ct);
            return;
        }

        if (!IsAllowedOrigin(context.Request.Headers["Origin"]))
        {
            await WritePlainJsonAsync(
                context.Response,
                403,
                new { error = "Forbidden origin for local MCP server." },
                ct);
            return;
        }

        try
        {
            if (path == "/mcp")
            {
                if (method == "POST")
                {
                    await HandleMcpPostAsync(context, ct);
                    return;
                }

                if (method == "GET")
                {
                    await WritePlainJsonAsync(
                        context.Response,
                        405,
                        new { error = "SSE stream is not implemented. Use POST /mcp for JSON-RPC requests." },
                        ct);
                    return;
                }
            }

            var (statusCode, body) = (path, method) switch
            {
                ("/mcp/tools/list", "GET") => HandleToolsList(),
                ("/mcp/tools/call", "POST") => await HandleToolCallAsync(context.Request, ct),
                ("/mcp/info", "GET") => HandleInfo(),
                _ => (404, JsonSerializer.Serialize(new { error = "Not found" }))
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes(body);
            await context.Response.OutputStream.WriteAsync(bytes, ct);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            await WritePlainJsonAsync(context.Response, 500, new { error = ex.Message }, ct);
        }
    }

    private async Task HandleMcpPostAsync(HttpListenerContext context, CancellationToken ct)
    {
        if (!AcceptsMcpJsonResponse(context.Request.Headers["Accept"]))
        {
            await WritePlainJsonAsync(
                context.Response,
                406,
                new { error = "MCP POST requires Accept to include application/json." },
                ct);
            return;
        }

        if (!IsSupportedPostProtocolVersion(context.Request.Headers["MCP-Protocol-Version"]))
        {
            await WritePlainJsonAsync(
                context.Response,
                400,
                new
                {
                    error = "Unsupported MCP-Protocol-Version.",
                    supported = SupportedProtocolVersions.ToArray()
                },
                ct);
            return;
        }

        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync(ct);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            await WriteJsonRpcEnvelopeAsync(
                context.Response,
                400,
                new JsonRpcResponse(
                    Id: null,
                    Error: new JsonRpcError(-32700, "Parse error: request body is not valid JSON.")),
                ct);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (IsNotification(root))
            {
                context.Response.StatusCode = 202;
                context.Response.ContentLength64 = 0;
                context.Response.Close();
                return;
            }

            var id = TryReadJsonRpcId(root);
            var response = await HandleJsonRpcRequestAsync(root, id, ct);
            await WriteJsonRpcEnvelopeAsync(context.Response, 200, response, ct);
        }
    }

    private (int, string) HandleInfo()
    {
        var info = new
        {
            name = "hermes-desktop",
            version = ServerVersion,
            capabilities = new { tools = true },
            toolCount = _tools.Count
        };
        return (200, JsonSerializer.Serialize(info, JsonOptions));
    }

    private (int, string) HandleToolsList()
    {
        return (200, JsonSerializer.Serialize(CreateToolsListResult(), JsonOptions));
    }

    private async Task<(int, string)> HandleToolCallAsync(HttpListenerRequest request, CancellationToken ct)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync(ct);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("name", out var nameEl))
            return (400, JsonSerializer.Serialize(new { error = "Missing 'name' field" }, JsonOptions));

        var toolName = nameEl.GetString()!;
        if (!_tools.TryGetValue(toolName, out var tool))
            return (404, JsonSerializer.Serialize(new { error = $"Tool '{toolName}' not found" }, JsonOptions));

        // Deserialize arguments
        var args = root.TryGetProperty("arguments", out var argsEl)
            ? JsonSerializer.Deserialize(argsEl.GetRawText(), tool.ParametersType)
            : Activator.CreateInstance(tool.ParametersType);

        if (args is null)
            return (400, JsonSerializer.Serialize(new { error = "Failed to parse arguments" }, JsonOptions));

        var result = await tool.ExecuteAsync(args, ct);

        var response = new
        {
            content = new[]
            {
                new { type = "text", text = result.Content }
            },
            isError = !result.Success
        };

        return (200, JsonSerializer.Serialize(response, JsonOptions));
    }

    private async Task<JsonRpcResponse> HandleJsonRpcRequestAsync(JsonElement root, string? id, CancellationToken ct)
    {
        if (!IsValidJsonRpcRequest(root, out var validationError))
        {
            return JsonRpcErrorResponse(id, -32600, validationError);
        }

        var method = root.GetProperty("method").GetString();
        var parameters = root.TryGetProperty("params", out var paramsProperty)
            ? paramsProperty
            : (JsonElement?)null;

        return method switch
        {
            "initialize" => HandleInitialize(id, parameters),
            "tools/list" => JsonRpcResultResponse(id, CreateToolsListResult()),
            "tools/call" => await HandleMcpToolCallAsync(id, parameters, ct),
            _ => JsonRpcErrorResponse(id, -32601, $"Method not found: {method}")
        };
    }

    private static JsonRpcResponse HandleInitialize(string? id, JsonElement? parameters)
    {
        var requested = TryReadProtocolVersion(parameters);
        if (requested is not null && string.IsNullOrWhiteSpace(requested))
        {
            return JsonRpcErrorResponse(
                id,
                -32602,
                "Invalid protocolVersion.",
                new { supported = SupportedProtocolVersions.ToArray(), requested });
        }

        var selected = requested is not null && SupportedProtocolVersions.Contains(requested)
            ? requested
            : PreferredProtocolVersion;

        return JsonRpcResultResponse(id, new
        {
            protocolVersion = selected,
            capabilities = new
            {
                tools = new
                {
                    listChanged = false
                }
            },
            serverInfo = new
            {
                name = ServerName,
                version = ServerVersion
            }
        });
    }

    private object CreateToolsListResult()
    {
        var tools = _tools.Values.Select(tool => new
        {
            name = tool.Name,
            description = tool.Description,
            inputSchema = GetInputSchema(tool)
        }).ToArray();

        return new { tools };
    }

    private async Task<JsonRpcResponse> HandleMcpToolCallAsync(string? id, JsonElement? parameters, CancellationToken ct)
    {
        if (parameters is null || parameters.Value.ValueKind != JsonValueKind.Object)
            return JsonRpcErrorResponse(id, -32602, "tools/call params must be an object.");

        if (!parameters.Value.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            return JsonRpcErrorResponse(id, -32602, "tools/call requires a non-empty string name.");
        }

        var toolName = nameElement.GetString()!;
        if (!_tools.TryGetValue(toolName, out var tool))
            return JsonRpcErrorResponse(id, -32602, $"Tool '{toolName}' not found.");

        object? arguments;
        try
        {
            arguments = parameters.Value.TryGetProperty("arguments", out var argsElement) &&
                        !IsEmptyObject(argsElement)
                ? JsonSerializer.Deserialize(argsElement.GetRawText(), tool.ParametersType, JsonOptions)
                : Activator.CreateInstance(tool.ParametersType);
        }
        catch (JsonException ex)
        {
            return JsonRpcErrorResponse(id, -32602, $"Invalid arguments for tool '{toolName}': {ex.Message}");
        }

        if (arguments is null)
            return JsonRpcErrorResponse(id, -32602, $"Invalid arguments for tool '{toolName}'.");

        ToolResult result;
        try
        {
            result = await tool.ExecuteAsync(arguments, ct);
        }
        catch (Exception ex)
        {
            result = ToolResult.Fail($"Tool '{toolName}' execution failed: {ex.Message}", ex);
        }

        return JsonRpcResultResponse(id, new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = result.Content
                }
            },
            isError = !result.Success
        });
    }

    private static JsonElement GetInputSchema(ITool tool)
    {
        if (tool is IToolSchemaProvider schemaProvider)
            return schemaProvider.GetParameterSchema();

        return EmptyObject.Clone();
    }

    private static bool IsEmptyObject(JsonElement element)
        => element.ValueKind == JsonValueKind.Object && !element.EnumerateObject().Any();

    private static bool IsValidJsonRpcRequest(JsonElement root, out string error)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            error = "JSON-RPC request must be an object.";
            return false;
        }

        if (!root.TryGetProperty("jsonrpc", out var jsonRpcElement) ||
            jsonRpcElement.ValueKind != JsonValueKind.String ||
            jsonRpcElement.GetString() != "2.0")
        {
            error = "JSON-RPC request requires jsonrpc='2.0'.";
            return false;
        }

        if (!root.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(methodElement.GetString()))
        {
            error = "JSON-RPC request requires a non-empty string method.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsNotification(JsonElement root)
        => root.ValueKind == JsonValueKind.Object &&
           root.TryGetProperty("jsonrpc", out var jsonRpcElement) &&
           jsonRpcElement.ValueKind == JsonValueKind.String &&
           jsonRpcElement.GetString() == "2.0" &&
           root.TryGetProperty("method", out var methodElement) &&
           methodElement.ValueKind == JsonValueKind.String &&
           !root.TryGetProperty("id", out _);

    private static string? TryReadJsonRpcId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idElement))
            return null;

        return idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString(),
            JsonValueKind.Number => idElement.GetRawText(),
            JsonValueKind.Null => null,
            _ => idElement.GetRawText()
        };
    }

    private static string? TryReadProtocolVersion(JsonElement? parameters)
    {
        if (parameters is null ||
            parameters.Value.ValueKind != JsonValueKind.Object ||
            !parameters.Value.TryGetProperty("protocolVersion", out var versionElement))
        {
            return null;
        }

        return versionElement.ValueKind == JsonValueKind.String
            ? versionElement.GetString()
            : string.Empty;
    }

    private static bool IsSupportedPostProtocolVersion(string? value)
        => string.IsNullOrWhiteSpace(value) || SupportedProtocolVersions.Contains(value.Trim());

    private static bool AcceptsMcpJsonResponse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
                         part.StartsWith("*/*", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonRpcResponse JsonRpcResultResponse(string? id, object result)
        => new(
            Id: id,
            Result: JsonSerializer.SerializeToElement(result, JsonOptions));

    private static JsonRpcResponse JsonRpcErrorResponse(string? id, int code, string message, object? data = null)
        => new(
            Id: id,
            Error: new JsonRpcError(
                code,
                message,
                data is null ? null : JsonSerializer.SerializeToElement(data, JsonOptions)));

    private static async Task WriteJsonRpcEnvelopeAsync(
        HttpListenerResponse response,
        int statusCode,
        JsonRpcResponse payload,
        CancellationToken ct)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(CreateJsonRpcResponseJson(payload));
        await response.OutputStream.WriteAsync(bytes, ct);
        response.Close();
    }

    private static string CreateJsonRpcResponseJson(JsonRpcResponse response)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["jsonrpc"] = response.JsonRpc,
            ["id"] = response.Id
        };

        if (response.Error is not null)
            envelope["error"] = response.Error;
        else
            envelope["result"] = response.Result ?? EmptyObject;

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private static async Task WritePlainJsonAsync(
        HttpListenerResponse response,
        int statusCode,
        object payload,
        CancellationToken ct)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        await response.OutputStream.WriteAsync(bytes, ct);
        response.Close();
    }

    public async Task StopAsync()
    {
        _running = false;
        _cts?.Cancel();
        _listener?.Stop();
        _logger.LogInformation("MCP server stopped");
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _listener?.Close();
        _cts?.Dispose();
    }
}
