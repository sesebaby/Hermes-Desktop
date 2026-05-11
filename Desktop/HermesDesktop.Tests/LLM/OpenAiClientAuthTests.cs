using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.LLM;

[TestClass]
public class OpenAiClientAuthTests
{
    [TestMethod]
    public async Task CompleteAsync_UsesEnvBackedOAuthProxyHeader()
    {
        const string envVarName = "HERMES_TEST_PROXY_TOKEN";
        Environment.SetEnvironmentVariable(envVarName, "oauth-token");

        try
        {
            HttpRequestMessage? capturedRequest = null;
            using var httpClient = new HttpClient(new CaptureHandler(request =>
            {
                capturedRequest = request;
                return CreateSuccessResponse();
            }));

            var client = new OpenAiClient(
                new LlmConfig
                {
                    Provider = "openai",
                    Model = "gpt-5.4",
                    BaseUrl = "https://proxy.example/v1",
                    AuthMode = "oauth_proxy_env",
                    AuthHeader = "Authorization",
                    AuthScheme = "Bearer",
                    AuthTokenEnv = envVarName
                },
                httpClient);

            var result = await client.CompleteAsync(
                new[] { new Message { Role = "user", Content = "hello" } },
                CancellationToken.None);

            Assert.AreEqual("ok", result);
            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual("Bearer oauth-token", capturedRequest!.Headers.Authorization!.ToString());
            Assert.IsNull(httpClient.DefaultRequestHeaders.Authorization);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [TestMethod]
    public async Task CompleteAsync_UsesCommandBackedOAuthProxyCustomHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new CaptureHandler(request =>
        {
            capturedRequest = request;
            return CreateSuccessResponse();
        }));

        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "openai",
                Model = "gpt-5.4",
                BaseUrl = "https://proxy.example/v1",
                AuthMode = "oauth_proxy_command",
                AuthHeader = "X-Proxy-Auth",
                AuthScheme = "",
                AuthTokenCommand = "echo cmd-token"
            },
            httpClient);

        var result = await client.CompleteAsync(
            new[] { new Message { Role = "user", Content = "hello" } },
            CancellationToken.None);

        Assert.AreEqual("ok", result);
        Assert.IsNotNull(capturedRequest);
        Assert.IsTrue(capturedRequest!.Headers.TryGetValues("X-Proxy-Auth", out var headerValues));
        CollectionAssert.AreEqual(new[] { "cmd-token" }, headerValues!.ToArray());
        Assert.IsNull(capturedRequest.Headers.Authorization);
        Assert.IsFalse(httpClient.DefaultRequestHeaders.Contains("X-Proxy-Auth"));
        Assert.IsNull(httpClient.DefaultRequestHeaders.Authorization);
    }

    [TestMethod]
    public async Task StreamAsync_StreamEvent_AppliesOAuthProxyEnvToOutgoingRequest()
    {
        const string envVarName = "HERMES_TEST_PROXY_TOKEN_STREAM";
        Environment.SetEnvironmentVariable(envVarName, "stream-oauth-token");

        try
        {
            HttpRequestMessage? capturedRequest = null;
            var sse =
                """
                data: {"choices":[{"delta":{"content":"z"}}]}

                data: [DONE]

                """;
            using var httpClient = new HttpClient(new CaptureHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(sse)))
                };
            }));

            var client = new OpenAiClient(
                new LlmConfig
                {
                    Provider = "openai",
                    Model = "gpt-5.4",
                    BaseUrl = "https://proxy.example/v1",
                    AuthMode = "oauth_proxy_env",
                    AuthHeader = "Authorization",
                    AuthScheme = "Bearer",
                    AuthTokenEnv = envVarName
                },
                httpClient);

            await foreach (var _ in client.StreamAsync(
                               null,
                               new[] { new Message { Role = "user", Content = "hello" } },
                               null,
                               CancellationToken.None))
            {
                // Drain SSE until completion
            }

            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual(
                "Bearer stream-oauth-token",
                capturedRequest!.Headers.Authorization!.ToString());
            Assert.IsNull(httpClient.DefaultRequestHeaders.Authorization);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [TestMethod]
    public async Task StreamAsync_Text_DoesNotMutateSharedHttpClientDefaultHeaders()
    {
        const string envVarName = "HERMES_TEST_PROXY_TOKEN_TEXT_SHARED";
        Environment.SetEnvironmentVariable(envVarName, "request-token");

        try
        {
            HttpRequestMessage? capturedRequest = null;
            var sse =
                """
                data: {"choices":[{"delta":{"content":"z"}}]}

                data: [DONE]

                """;
            using var httpClient = new HttpClient(new CaptureHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(sse)))
                };
            }));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "shared-default-token");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Shared-Header", "shared-default-value");

            var client = new OpenAiClient(
                new LlmConfig
                {
                    Provider = "openai",
                    Model = "gpt-5.4",
                    BaseUrl = "https://proxy.example/v1",
                    AuthMode = "oauth_proxy_env",
                    AuthHeader = "Authorization",
                    AuthScheme = "Bearer",
                    AuthTokenEnv = envVarName
                },
                httpClient);

            await foreach (var _ in client.StreamAsync(
                               new[] { new Message { Role = "user", Content = "hello" } },
                               CancellationToken.None))
            {
                // Drain SSE until completion
            }

            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual("Bearer request-token", capturedRequest!.Headers.Authorization!.ToString());
            Assert.AreEqual("Bearer shared-default-token", httpClient.DefaultRequestHeaders.Authorization!.ToString());
            Assert.IsTrue(httpClient.DefaultRequestHeaders.TryGetValues("X-Shared-Header", out var sharedValues));
            CollectionAssert.AreEqual(new[] { "shared-default-value" }, sharedValues!.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [TestMethod]
    public async Task StreamAsync_StreamEvent_DoesNotMutateSharedHttpClientDefaultHeaders()
    {
        const string envVarName = "HERMES_TEST_PROXY_TOKEN_EVENT_SHARED";
        Environment.SetEnvironmentVariable(envVarName, "request-token");

        try
        {
            HttpRequestMessage? capturedRequest = null;
            var sse =
                """
                data: {"choices":[{"delta":{"content":"z"}}]}

                data: [DONE]

                """;
            using var httpClient = new HttpClient(new CaptureHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(sse)))
                };
            }));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "shared-default-token");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Shared-Header", "shared-default-value");

            var client = new OpenAiClient(
                new LlmConfig
                {
                    Provider = "openai",
                    Model = "gpt-5.4",
                    BaseUrl = "https://proxy.example/v1",
                    AuthMode = "oauth_proxy_env",
                    AuthHeader = "Authorization",
                    AuthScheme = "Bearer",
                    AuthTokenEnv = envVarName
                },
                httpClient);

            await foreach (var _ in client.StreamAsync(
                               null,
                               new[] { new Message { Role = "user", Content = "hello" } },
                               null,
                               CancellationToken.None))
            {
                // Drain SSE until completion
            }

            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual("Bearer request-token", capturedRequest!.Headers.Authorization!.ToString());
            Assert.AreEqual("Bearer shared-default-token", httpClient.DefaultRequestHeaders.Authorization!.ToString());
            Assert.IsTrue(httpClient.DefaultRequestHeaders.TryGetValues("X-Shared-Header", out var sharedValues));
            CollectionAssert.AreEqual(new[] { "shared-default-value" }, sharedValues!.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [TestMethod]
    public async Task StreamAsync_WithSystemPromptAndTools_IncludesBothInRequestPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var sse =
            """
            data: {"choices":[{"delta":{"content":"z"}}]}

            data: [DONE]

            """;
        using var httpClient = new HttpClient(new CaptureHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(sse)))
            };
        }));

        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "openai",
                Model = "gpt-5.4",
                BaseUrl = "https://proxy.example/v1",
                AuthMode = "none"
            },
            httpClient);

        var tools = new[]
        {
            new ToolDefinition
            {
                Name = "lookup",
                Description = "Lookup a fact",
                Parameters = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                })
            }
        };

        await foreach (var _ in client.StreamAsync(
                           "You are a local NPC.",
                           new[] { new Message { Role = "user", Content = "hello" } },
                           tools,
                           CancellationToken.None))
        {
            // Drain SSE until completion
        }

        Assert.IsNotNull(capturedRequest);
        Assert.IsNotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);

        Assert.IsTrue(doc.RootElement.TryGetProperty("messages", out var messages));
        Assert.AreEqual("system", messages[0].GetProperty("role").GetString());
        Assert.AreEqual("You are a local NPC.", messages[0].GetProperty("content").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("tools", out var toolsElement));
        Assert.AreEqual("lookup", toolsElement[0].GetProperty("function").GetProperty("name").GetString());
        Assert.AreEqual("auto", doc.RootElement.GetProperty("tool_choice").GetString());
    }

    [TestMethod]
    public async Task StreamAsync_WithConfiguredTemperature_UsesConfiguredTemperatureInPayload()
    {
        string? capturedBody = null;
        var sse =
            """
            data: {"choices":[{"delta":{"content":"z"},"finish_reason":"stop"}]}

            data: [DONE]

            """;
        using var httpClient = new HttpClient(new CaptureHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(sse)))
            };
        }));

        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "openai",
                Model = "gpt-5.4",
                BaseUrl = "https://proxy.example/v1",
                AuthMode = "none",
                Temperature = 0.1
            },
            httpClient);

        await foreach (var _ in client.StreamAsync(
                           "You are deterministic.",
                           new[] { new Message { Role = "user", Content = "hello" } },
                           null,
                           CancellationToken.None))
        {
            // Drain SSE until completion
        }

        Assert.IsNotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.AreEqual(0.1, doc.RootElement.GetProperty("temperature").GetDouble(), 0.0001);
    }

    [TestMethod]
    public async Task CompleteAsync_WithJsonObjectResponseFormat_IncludesResponseFormatInPayload()
    {
        string? capturedBody = null;
        using var httpClient = new HttpClient(new CaptureHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreateSuccessResponse();
        }));

        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "deepseek",
                Model = "deepseek-v4-flash",
                BaseUrl = "https://api.deepseek.com/v1",
                AuthMode = "none",
                ResponseFormat = "json_object"
            },
            httpClient);

        await client.CompleteAsync(
            new[] { new Message { Role = "user", Content = "Return json: {\"action\":\"wait\"}" } },
            CancellationToken.None);

        Assert.IsNotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var responseFormat = doc.RootElement.GetProperty("response_format");
        Assert.AreEqual("json_object", responseFormat.GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task CompleteAsync_WithJsonObjectResponseFormat_AddsJsonInstructionWhenPromptDoesNotMentionJson()
    {
        string? capturedBody = null;
        using var httpClient = new HttpClient(new CaptureHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreateSuccessResponse();
        }));

        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "deepseek",
                Model = "deepseek-v4-flash",
                BaseUrl = "https://api.deepseek.com/v1",
                AuthMode = "none",
                ResponseFormat = "json_object"
            },
            httpClient);

        await client.CompleteWithToolsAsync(
            [new Message { Role = "user", Content = "你被唤醒了一轮。自己决定下一步。" }],
            [
                new ToolDefinition
                {
                    Name = "stardew_status",
                    Description = "Read status.",
                    Parameters = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new { },
                        required = Array.Empty<string>()
                    })
                }
            ],
            CancellationToken.None);

        Assert.IsNotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.AreEqual("system", messages[0].GetProperty("role").GetString());
        StringAssert.Contains(messages[0].GetProperty("content").GetString(), "json");
        Assert.AreEqual("user", messages[1].GetProperty("role").GetString());
    }

    [TestMethod]
    public async Task StreamAsync_ToolCallDeltas_EmitToolUseEventsWithCompleteArguments()
    {
        var sse =
            """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"stardew_move","arguments":""}}]}}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"destination\":\"PierreShop\""}}]}}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":",\"reason\":\"meet player\"}"}}]},"finish_reason":"tool_calls"}]}

            data: [DONE]

            """;
        using var httpClient = new HttpClient(new CaptureHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(sse)))
            }));
        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "openai",
                Model = "gpt-5.4",
                BaseUrl = "https://proxy.example/v1",
                AuthMode = "none"
            },
            httpClient);
        var tools = new[]
        {
            new ToolDefinition
            {
                Name = "stardew_move",
                Description = "Move an NPC",
                Parameters = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        destination = new { type = "string" },
                        reason = new { type = "string" }
                    },
                    required = new[] { "destination" }
                })
            }
        };

        var events = new List<StreamEvent>();
        await foreach (var streamEvent in client.StreamAsync(
                           "You execute local Stardew actions.",
                           new[] { new Message { Role = "user", Content = "move Haley" } },
                           tools,
                           CancellationToken.None))
        {
            events.Add(streamEvent);
        }

        var start = events.OfType<StreamEvent.ToolUseStart>().Single();
        var complete = events.OfType<StreamEvent.ToolUseComplete>().Single();

        Assert.AreEqual("call_1", start.Id);
        Assert.AreEqual("stardew_move", start.Name);
        Assert.AreEqual("call_1", complete.Id);
        Assert.AreEqual("stardew_move", complete.Name);
        Assert.AreEqual("PierreShop", complete.Arguments.GetProperty("destination").GetString());
        Assert.AreEqual("meet player", complete.Arguments.GetProperty("reason").GetString());
        Assert.AreEqual(
            "tool_calls",
            events.OfType<StreamEvent.MessageComplete>().Single().StopReason);
    }

    [TestMethod]
    public async Task CompleteWithToolsAsync_BadRequestExceptionIncludesResponseBody()
    {
        using var httpClient = new HttpClient(new CaptureHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"message":"Invalid parameter: messages with role 'tool' must follow a tool call."}}""",
                    Encoding.UTF8,
                    "application/json")
            }));

        var client = new OpenAiClient(
            new LlmConfig
            {
                Provider = "openai",
                Model = "gpt-5.4",
                BaseUrl = "https://proxy.example/v1",
                ApiKey = "test-key"
            },
            httpClient);

        var ex = await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            client.CompleteWithToolsAsync(
                new[] { new Message { Role = "user", Content = "hello" } },
                Array.Empty<ToolDefinition>(),
                CancellationToken.None));

        StringAssert.Contains(ex.Message, "400");
        StringAssert.Contains(ex.Message, "Invalid parameter");
    }

    private static HttpResponseMessage CreateSuccessResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}]}
                """,
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
