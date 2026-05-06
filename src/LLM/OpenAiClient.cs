namespace Hermes.Agent.LLM;

using Hermes.Agent.Core;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

public sealed class OpenAiClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private readonly CredentialPool? _credentialPool;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAiClient(LlmConfig config, HttpClient httpClient, CredentialPool? credentialPool = null)
    {
        _config = config;
        _httpClient = httpClient;
        _credentialPool = credentialPool;
    }

    // ── Simple completion (backwards compatible) ──

    public async Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
    {
        var payload = BuildPayload(null, messages, tools: null, stream: false);
        using var response = await PostAsync(payload, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content").GetString() ?? "";
    }

    // ── Completion with tool calling ──

    public async Task<ChatResponse> CompleteWithToolsAsync(
        IEnumerable<Message> messages,
        IEnumerable<ToolDefinition> tools,
        CancellationToken ct)
    {
        var toolDefs = tools.Select(t => new
        {
            type = "function",
            function = new { name = t.Name, description = t.Description, parameters = t.Parameters }
        }).ToArray();

        var payload = BuildPayload(null, messages, toolDefs, stream: false);
        using var response = await PostAsync(payload, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        var choice = doc.RootElement.GetProperty("choices")[0];
        var msg = choice.GetProperty("message");
        var finishReason = choice.GetProperty("finish_reason").GetString();

        string? content = null;
        if (msg.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
            content = contentEl.GetString();

        var reasoningContent = ReadStringOrRawJson(msg, "reasoning_content");
        var reasoning = ReadStringOrRawJson(msg, "reasoning");
        var reasoningDetails = ReadStringOrRawJson(msg, "reasoning_details");
        var codexReasoningItems = ReadStringOrRawJson(msg, "codex_reasoning_items");
        UsageStats? usage = null;
        if (doc.RootElement.TryGetProperty("usage", out var usageElement))
        {
            var inputTokens = usageElement.TryGetProperty("prompt_tokens", out var promptTokensEl)
                ? promptTokensEl.GetInt32()
                : 0;
            var outputTokens = usageElement.TryGetProperty("completion_tokens", out var completionTokensEl)
                ? completionTokensEl.GetInt32()
                : 0;
            int? cacheReadTokens = null;
            if (usageElement.TryGetProperty("prompt_tokens_details", out var promptTokensDetails) &&
                promptTokensDetails.TryGetProperty("cached_tokens", out var cachedTokensEl))
            {
                cacheReadTokens = cachedTokensEl.GetInt32();
            }

            usage = new UsageStats(inputTokens, outputTokens, null, cacheReadTokens);
        }

        // Fallback: reasoning models (MiniMax, DeepSeek-R1, etc.) may put text in "reasoning" with empty "content"
        if (string.IsNullOrEmpty(content) &&
            !string.IsNullOrEmpty(reasoning))
        {
            content = reasoning;
        }

        List<ToolCall>? toolCalls = null;
        if (msg.TryGetProperty("tool_calls", out var toolCallsEl) && toolCallsEl.ValueKind == JsonValueKind.Array)
        {
            toolCalls = new List<ToolCall>();
            foreach (var tc in toolCallsEl.EnumerateArray())
            {
                var fn = tc.GetProperty("function");
                toolCalls.Add(new ToolCall
                {
                    Id = tc.GetProperty("id").GetString()!,
                    Name = fn.GetProperty("name").GetString()!,
                    Arguments = fn.GetProperty("arguments").GetString() ?? "{}"
                });
            }
        }

        return new ChatResponse
        {
            Content = content,
            ToolCalls = toolCalls,
            FinishReason = finishReason,
            Reasoning = reasoning,
            ReasoningContent = reasoningContent,
            ReasoningDetails = reasoningDetails,
            CodexReasoningItems = codexReasoningItems,
            Usage = usage
        };
    }

    // ── Streaming completion ──

    public async IAsyncEnumerable<string> StreamAsync(
        IEnumerable<Message> messages,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = BuildPayload(null, messages, tools: null, stream: true);
        var json = JsonSerializer.Serialize(payload);
        using var request = await CreateRequestAsync($"{_config.BaseUrl}/chat/completions", json, ct);

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            response?.Dispose();
            throw;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            response?.Dispose();
            throw new TimeoutException("Request timed out - the LLM server may be overloaded or unreachable.");
        }

        var httpResponse = response!;
        using (httpResponse)
        {
            var body = httpResponse.Content;
            if (body is null)
            {
                Debug.WriteLine("OpenAiClient.StreamTextAsync: response.Content is null.");
                throw new InvalidOperationException("Empty response body from server.");
            }

            using var stream = await body.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (IOException ex)
                {
                    throw new IOException("Connection lost during streaming.", ex);
                }

                if (line is null) break;
                if (!line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]") break;

                JsonDocument? chunk;
                try
                {
                    chunk = JsonDocument.Parse(data);
                }
                catch (JsonException)
                {
                    continue; // Skip malformed chunks
                }

                using (chunk)
                {
                    if (!chunk.RootElement.TryGetProperty("choices", out var choices) ||
                        choices.GetArrayLength() == 0) continue;

                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var contentEl) &&
                        contentEl.ValueKind == JsonValueKind.String)
                    {
                        var token = contentEl.GetString();
                        if (!string.IsNullOrEmpty(token))
                            yield return token;
                    }
                }
            }
        }
    }

    // ── Helpers ──

    private object BuildPayload(string? systemPrompt, IEnumerable<Message> messages, object? tools, bool stream)
    {
        var convertedMessages = messages.Select(m =>
        {
            // Tool result message
            if (m.Role == "tool")
                return (object)new { role = "tool", content = m.Content, tool_call_id = m.ToolCallId };

            // Assistant message with tool calls
            if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
                return new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = m.Content ?? (object?)null,
                    ["tool_calls"] = m.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.Name, arguments = tc.Arguments }
                    }).ToArray(),
                    ["reasoning"] = string.IsNullOrEmpty(m.Reasoning) ? null : m.Reasoning,
                    ["reasoning_content"] = string.IsNullOrEmpty(m.ReasoningContent) ? null : m.ReasoningContent,
                    ["reasoning_details"] = string.IsNullOrEmpty(m.ReasoningDetails) ? null : m.ReasoningDetails,
                    ["codex_reasoning_items"] = string.IsNullOrEmpty(m.CodexReasoningItems) ? null : m.CodexReasoningItems
                }.Where(kvp => kvp.Value is not null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Regular message
            if (m.Role == "assistant" &&
                (!string.IsNullOrEmpty(m.Reasoning) ||
                 !string.IsNullOrEmpty(m.ReasoningContent) ||
                 !string.IsNullOrEmpty(m.ReasoningDetails) ||
                 !string.IsNullOrEmpty(m.CodexReasoningItems)))
            {
                return new Dictionary<string, object?>
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content,
                    ["reasoning"] = string.IsNullOrEmpty(m.Reasoning) ? null : m.Reasoning,
                    ["reasoning_content"] = string.IsNullOrEmpty(m.ReasoningContent) ? null : m.ReasoningContent,
                    ["reasoning_details"] = string.IsNullOrEmpty(m.ReasoningDetails) ? null : m.ReasoningDetails,
                    ["codex_reasoning_items"] = string.IsNullOrEmpty(m.CodexReasoningItems) ? null : m.CodexReasoningItems
                }.Where(kvp => kvp.Value is not null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            return (object)new { role = m.Role, content = m.Content };
        });
        var msgs = string.IsNullOrWhiteSpace(systemPrompt)
            ? convertedMessages.ToArray()
            : new[] { (object)new { role = "system", content = systemPrompt.Trim() } }
                .Concat(convertedMessages)
                .ToArray();

        if (tools is not null)
        {
            return new
            {
                model = _config.Model,
                messages = msgs,
                tools,
                tool_choice = "auto",
                temperature = 0.7,
                stream
            };
        }

        return new
        {
            model = _config.Model,
            messages = msgs,
            temperature = 0.7,
            stream
        };
    }

    private async Task<HttpResponseMessage> PostAsync(object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var url = $"{_config.BaseUrl}/chat/completions";
        var llmRequestId = $"req_llm_{Guid.NewGuid():N}";

        // If credential pool is available, use it with retry on 401
        if (UsesApiKeyAuth && _credentialPool is not null && _credentialPool.HasHealthyCredentials)
        {
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var apiKey = _credentialPool.GetNext();
                if (apiKey is null) break;

                using var request = await CreateRequestAsync(url, json, ct, apiKey);
                var response = await _httpClient.SendAsync(request, ct);

                // INV-004/005: Mark credential failed on auth or rate-limit errors and rotate
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _credentialPool.MarkFailed(apiKey, (int)response.StatusCode, "auth_error");
                    response.Dispose();
                    continue; // Retry with next key
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _credentialPool.MarkFailed(apiKey, 429, "rate_limited");
                    response.Dispose();
                    continue; // Retry with next key
                }

                await EnsureSuccessStatusCodeAsync(response, ct, llmRequestId, _config);
                return response;
            }
        }

        // Fallback: use default header auth
        using var fallbackRequest = await CreateRequestAsync(url, json, ct);
        var fallbackResponse = await _httpClient.SendAsync(fallbackRequest, ct);
        await EnsureSuccessStatusCodeAsync(fallbackResponse, ct, llmRequestId, _config);
        return fallbackResponse;
    }

    private static string? ReadStringOrRawJson(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.GetRawText();
    }

    private static async Task EnsureSuccessStatusCodeAsync(
        HttpResponseMessage response,
        CancellationToken ct,
        string llmRequestId,
        LlmConfig config)
    {
        if (response.IsSuccessStatusCode)
            return;

        var statusCode = response.StatusCode;
        var reasonPhrase = response.ReasonPhrase;
        var providerRequestId = ReadProviderRequestId(response);
        var body = response.Content is null
            ? ""
            : await response.Content.ReadAsStringAsync(ct);
        response.Dispose();

        var reasoningContentError = body.Contains("reasoning_content", StringComparison.OrdinalIgnoreCase);
        var message = $"Response status code does not indicate success: {(int)statusCode} ({reasonPhrase}). " +
                      $"llmRequestId={llmRequestId};providerRequestId={providerRequestId ?? "-"};" +
                      $"reasoning_content_error={reasoningContentError.ToString().ToLowerInvariant()};" +
                      $"provider={config.Provider};baseUrl={config.BaseUrl};model={config.Model};";
        if (!string.IsNullOrWhiteSpace(body))
            message += $" Body: {TruncateErrorBody(body)}";

        throw new HttpRequestException(message, null, statusCode);
    }

    private static string? ReadProviderRequestId(HttpResponseMessage response)
    {
        foreach (var header in new[] { "x-request-id", "openai-request-id", "request-id" })
        {
            if (response.Headers.TryGetValues(header, out var values))
                return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        return null;
    }

    private static string TruncateErrorBody(string body)
    {
        const int maxChars = 2000;
        return body.Length <= maxChars
            ? body
            : body[..maxChars] + "...";
    }

    private bool UsesApiKeyAuth =>
        string.IsNullOrWhiteSpace(_config.AuthMode) ||
        string.Equals(_config.AuthMode, "api_key", StringComparison.OrdinalIgnoreCase);

    private async Task<HttpRequestMessage> CreateRequestAsync(
        string url,
        string json,
        CancellationToken ct,
        string? apiKeyOverride = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Keep authentication request-scoped so callers can safely reuse HttpClient
        // instances without leaking credentials through DefaultRequestHeaders.
        await ApplyAuthenticationAsync(request, apiKeyOverride, ct);
        return request;
    }

    private async Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        string? apiKeyOverride,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            AddHeader(request, "Authorization", "Bearer", apiKeyOverride.Trim());
            return;
        }

        var authMode = (_config.AuthMode ?? "api_key").Trim().ToLowerInvariant();
        switch (authMode)
        {
            case "":
            case "api_key":
                if (!string.IsNullOrWhiteSpace(_config.ApiKey))
                    AddHeader(request, "Authorization", "Bearer", _config.ApiKey.Trim());
                return;

            case "api_key_env":
            {
                var envName = _config.ApiKeyEnv?.Trim();
                if (string.IsNullOrWhiteSpace(envName))
                    throw new InvalidOperationException("model.api_key_env is required for auth_mode=api_key_env.");

                var apiKey = Environment.GetEnvironmentVariable(envName);
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException($"Environment variable '{envName}' is not set for API key auth.");

                AddHeader(request, "Authorization", "Bearer", apiKey.Trim());
                return;
            }

            case "none":
                return;

            case "oauth_proxy_env":
            {
                var envName = _config.AuthTokenEnv?.Trim();
                if (string.IsNullOrWhiteSpace(envName))
                    throw new InvalidOperationException("model.auth_token_env is required for auth_mode=oauth_proxy_env.");

                var token = Environment.GetEnvironmentVariable(envName);
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException($"Environment variable '{envName}' is not set for OAuth proxy auth.");

                AddConfiguredProxyHeader(request, token.Trim());
                return;
            }

            case "oauth_proxy_command":
            {
                var command = _config.AuthTokenCommand?.Trim();
                if (string.IsNullOrWhiteSpace(command))
                    throw new InvalidOperationException("model.auth_token_command is required for auth_mode=oauth_proxy_command.");

                var token = await ExecuteTokenCommandAsync(command, ct);
                AddConfiguredProxyHeader(request, token);
                return;
            }

            default:
                throw new InvalidOperationException($"Unsupported model.auth_mode '{_config.AuthMode}'.");
        }
    }

    private void AddConfiguredProxyHeader(HttpRequestMessage request, string token)
    {
        var headerName = string.IsNullOrWhiteSpace(_config.AuthHeader)
            ? "Authorization"
            : _config.AuthHeader.Trim();
        var authScheme = _config.AuthScheme ?? "Bearer";

        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(authScheme))
        {
            throw new InvalidOperationException("model.auth_scheme must be set when model.auth_header is Authorization.");
        }

        AddHeader(request, headerName, authScheme, token);
    }

    private static void AddHeader(HttpRequestMessage request, string headerName, string? scheme, string token)
    {
        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(scheme))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme.Trim(), token);
            return;
        }

        var headerValue = string.IsNullOrWhiteSpace(scheme)
            ? token
            : $"{scheme.Trim()} {token}";

        request.Headers.Remove(headerName);
        request.Headers.TryAddWithoutValidation(headerName, headerValue);
    }

    private static async Task<string> ExecuteTokenCommandAsync(string command, CancellationToken ct)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"")
            : new ProcessStartInfo("/bin/sh", $"-lc \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"");

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"OAuth proxy token command failed with exit code {process.ExitCode}: {stderr}");

        if (string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException("OAuth proxy token command returned an empty token.");

        return stdout;
    }

    public async IAsyncEnumerable<StreamEvent> StreamAsync(
        string? systemPrompt,
        IEnumerable<Message> messages,
        IEnumerable<ToolDefinition>? tools = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Direct SSE parsing that handles both:
        // 1. <think>...</think> tags in content (QwQ, DeepSeek-R1 via Ollama)
        // 2. Separate "reasoning" JSON field (MiniMax-M2.7, etc.)
        var inThinkBlock = false;
        var contentBuffer = new StringBuilder();
        var stopReason = "stop";
        var pendingToolCalls = new SortedDictionary<int, PendingOpenAiToolCall>();

        var toolDefs = tools?.Select(t => new
        {
            type = "function",
            function = new { name = t.Name, description = t.Description, parameters = t.Parameters }
        }).ToArray();
        var payload = BuildPayload(systemPrompt, messages, toolDefs, stream: true);
        var json = JsonSerializer.Serialize(payload);
        using var request = await CreateRequestAsync($"{_config.BaseUrl}/chat/completions", json, ct);

        HttpResponseMessage? response = null;
        Exception? connectError = null;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) { response?.Dispose(); connectError = ex; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { response?.Dispose(); connectError = new TimeoutException("Request timed out"); }

        if (connectError is not null)
        {
            yield return new StreamEvent.StreamError(connectError);
            yield break;
        }

        var httpResponse = response!;
        using (httpResponse)
        {
            var body = httpResponse.Content;
            if (body is null)
            {
                Debug.WriteLine("OpenAiClient.StreamEventsAsync: response.Content is null.");
                yield return new StreamEvent.StreamError(
                    new InvalidOperationException("Empty response body from server."));
                yield break;
            }

            using var stream = await body.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!ct.IsCancellationRequested)
            {
                string? line;
                IOException? ioError = null;
                try { line = await reader.ReadLineAsync(ct); }
                catch (IOException ioEx) { line = null; ioError = ioEx; }

                if (ioError is not null)
                {
                    yield return new StreamEvent.StreamError(ioError);
                    yield break;
                }

                if (line is null) break;
                if (!line.StartsWith("data: ")) continue;
                var data = line["data: ".Length..];
                if (data == "[DONE]")
                {
                    foreach (var toolEvent in CompletePendingOpenAiToolCalls(pendingToolCalls))
                        yield return toolEvent;
                    break;
                }

                JsonDocument? chunk;
                try { chunk = JsonDocument.Parse(data); }
                catch (JsonException) { continue; }

                using (chunk)
                {
                    if (!chunk.RootElement.TryGetProperty("choices", out var choices) ||
                        choices.GetArrayLength() == 0) continue;

                    var choice = choices[0];
                    if (choice.TryGetProperty("finish_reason", out var finishReasonEl) &&
                        finishReasonEl.ValueKind == JsonValueKind.String)
                    {
                        stopReason = finishReasonEl.GetString() ?? stopReason;
                    }

                    if (!choice.TryGetProperty("delta", out var delta) ||
                        delta.ValueKind != JsonValueKind.Object)
                    {
                        if (string.Equals(stopReason, "tool_calls", StringComparison.Ordinal))
                        {
                            foreach (var toolEvent in CompletePendingOpenAiToolCalls(pendingToolCalls))
                                yield return toolEvent;
                        }

                        continue;
                    }

                    if (delta.TryGetProperty("tool_calls", out var toolCallsEl) &&
                        toolCallsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var toolCallEl in toolCallsEl.EnumerateArray())
                        {
                            var index = toolCallEl.TryGetProperty("index", out var indexEl) &&
                                indexEl.ValueKind == JsonValueKind.Number
                                    ? indexEl.GetInt32()
                                    : pendingToolCalls.Count;

                            if (!pendingToolCalls.TryGetValue(index, out var pendingToolCall))
                            {
                                pendingToolCall = new PendingOpenAiToolCall();
                                pendingToolCalls[index] = pendingToolCall;
                            }

                            if (toolCallEl.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String)
                            {
                                pendingToolCall.Id = idEl.GetString();
                            }

                            string? argumentsDelta = null;
                            if (toolCallEl.TryGetProperty("function", out var functionEl) &&
                                functionEl.ValueKind == JsonValueKind.Object)
                            {
                                if (functionEl.TryGetProperty("name", out var nameEl) &&
                                    nameEl.ValueKind == JsonValueKind.String)
                                {
                                    pendingToolCall.Name = nameEl.GetString();
                                }

                                if (functionEl.TryGetProperty("arguments", out var argumentsEl) &&
                                    argumentsEl.ValueKind == JsonValueKind.String)
                                {
                                    argumentsDelta = argumentsEl.GetString();
                                }
                            }

                            if (pendingToolCall.TryMarkStarted(out var id, out var name))
                                yield return new StreamEvent.ToolUseStart(id, name);

                            if (!string.IsNullOrEmpty(argumentsDelta))
                            {
                                pendingToolCall.Arguments.Append(argumentsDelta);
                                if (pendingToolCall.StartEmitted &&
                                    !string.IsNullOrWhiteSpace(pendingToolCall.Id))
                                {
                                    yield return new StreamEvent.ToolUseDelta(
                                        pendingToolCall.Id!,
                                        argumentsDelta);
                                }
                            }
                        }
                    }

                    // Extract reasoning field (MiniMax, DeepSeek-R1 JSON format)
                    if (delta.TryGetProperty("reasoning", out var reasoningEl) &&
                        reasoningEl.ValueKind == JsonValueKind.String)
                    {
                        var reasoning = reasoningEl.GetString();
                        if (!string.IsNullOrEmpty(reasoning))
                            yield return new StreamEvent.ThinkingDelta(reasoning);
                    }

                    // Extract content field
                    if (delta.TryGetProperty("content", out var contentEl) &&
                        contentEl.ValueKind == JsonValueKind.String)
                    {
                        var token = contentEl.GetString();
                        if (!string.IsNullOrEmpty(token))
                        {
                            // Handle <think>...</think> tags within content
                            contentBuffer.Append(token);

                            while (contentBuffer.Length > 0)
                            {
                                var text = contentBuffer.ToString();

                                if (!inThinkBlock)
                                {
                                    var openIdx = text.IndexOf("<think>");
                                    if (openIdx >= 0)
                                    {
                                        if (openIdx > 0)
                                            yield return new StreamEvent.TokenDelta(text[..openIdx]);
                                        inThinkBlock = true;
                                        contentBuffer.Clear();
                                        contentBuffer.Append(text[(openIdx + "<think>".Length)..]);
                                        continue;
                                    }

                                    // Check for partial <think> tag at end
                                    if (text.Length > 0 && (text[^1] == '<' || text.EndsWith("<t") ||
                                        text.EndsWith("<th") || text.EndsWith("<thi") ||
                                        text.EndsWith("<thin") || text.EndsWith("<think")))
                                    {
                                        var ps = text.LastIndexOf('<');
                                        if (ps >= 0 && ps < text.Length)
                                        {
                                            if (ps > 0) yield return new StreamEvent.TokenDelta(text[..ps]);
                                            contentBuffer.Clear();
                                            contentBuffer.Append(text[ps..]);
                                            break;
                                        }
                                    }

                                    yield return new StreamEvent.TokenDelta(text);
                                    contentBuffer.Clear();
                                }
                                else
                                {
                                    var closeIdx = text.IndexOf("</think>");
                                    if (closeIdx >= 0)
                                    {
                                        if (closeIdx > 0)
                                            yield return new StreamEvent.ThinkingDelta(text[..closeIdx]);
                                        inThinkBlock = false;
                                        contentBuffer.Clear();
                                        contentBuffer.Append(text[(closeIdx + "</think>".Length)..]);
                                        continue;
                                    }

                                    if (text.EndsWith("<") || text.EndsWith("</") || text.EndsWith("</t") ||
                                        text.EndsWith("</th") || text.EndsWith("</thi") ||
                                        text.EndsWith("</thin") || text.EndsWith("</think"))
                                    {
                                        var ps = text.LastIndexOf('<');
                                        if (ps > 0)
                                        {
                                            yield return new StreamEvent.ThinkingDelta(text[..ps]);
                                            contentBuffer.Clear();
                                            contentBuffer.Append(text[ps..]);
                                            break;
                                        }
                                        break;
                                    }

                                    yield return new StreamEvent.ThinkingDelta(text);
                                    contentBuffer.Clear();
                                }

                                break;
                            }
                        }
                    }

                    if (string.Equals(stopReason, "tool_calls", StringComparison.Ordinal))
                    {
                        foreach (var toolEvent in CompletePendingOpenAiToolCalls(pendingToolCalls))
                            yield return toolEvent;
                    }
                }
            }
        }

        // Flush remaining content buffer
        if (contentBuffer.Length > 0)
        {
            var remaining = contentBuffer.ToString();
            yield return inThinkBlock
                ? new StreamEvent.ThinkingDelta(remaining)
                : new StreamEvent.TokenDelta(remaining);
        }

        yield return new StreamEvent.MessageComplete(stopReason, new UsageStats(0, 0));
    }

    private static IEnumerable<StreamEvent> CompletePendingOpenAiToolCalls(
        SortedDictionary<int, PendingOpenAiToolCall> pendingToolCalls)
    {
        foreach (var toolCallEntry in pendingToolCalls)
        {
            var pendingToolCall = toolCallEntry.Value;
            if (pendingToolCall.Completed)
                continue;

            pendingToolCall.Completed = true;
            if (string.IsNullOrWhiteSpace(pendingToolCall.Id) ||
                string.IsNullOrWhiteSpace(pendingToolCall.Name))
            {
                yield return new StreamEvent.StreamError(
                    new InvalidOperationException(
                        $"Incomplete streaming tool call at index {toolCallEntry.Key}."));
                continue;
            }

            var rawArguments = pendingToolCall.Arguments.Length == 0
                ? "{}"
                : pendingToolCall.Arguments.ToString();
            StreamEvent toolEvent;
            try
            {
                using var argumentsDoc = JsonDocument.Parse(rawArguments);
                toolEvent = new StreamEvent.ToolUseComplete(
                    pendingToolCall.Id!,
                    pendingToolCall.Name!,
                    argumentsDoc.RootElement.Clone());
            }
            catch (JsonException ex)
            {
                toolEvent = new StreamEvent.StreamError(
                    new JsonException($"Invalid tool arguments: {rawArguments}", ex));
            }

            yield return toolEvent;
        }
    }

    private sealed class PendingOpenAiToolCall
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public StringBuilder Arguments { get; } = new();

        public bool StartEmitted { get; private set; }

        public bool Completed { get; set; }

        public bool TryMarkStarted(out string id, out string name)
        {
            id = Id ?? "";
            name = Name ?? "";
            if (StartEmitted ||
                string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            StartEmitted = true;
            return true;
        }
    }
}
