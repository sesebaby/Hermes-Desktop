using System.Net;
using System.Text;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.LLM;

[TestClass]
public class OpenAiClientReasoningTests
{
    [TestMethod]
    public async Task CompleteWithToolsAsync_PreservesReasoningFieldsFromAssistantToolCall()
    {
        using var httpClient = new HttpClient(new CaptureHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "role": "assistant",
                            "content": "",
                            "reasoning_content": "I need the inventory tool before answering.",
                            "reasoning": "{\"summary\":\"tool needed\"}",
                            "reasoning_details": "[{\"type\":\"summary_text\",\"text\":\"tool needed\"}]",
                            "codex_reasoning_items": "[{\"id\":\"rs_1\",\"type\":\"reasoning\"}]",
                            "tool_calls": [
                              {
                                "id": "call_inventory",
                                "type": "function",
                                "function": {
                                  "name": "inventory",
                                  "arguments": "{\"npc\":\"Haley\"}"
                                }
                              }
                            ]
                          },
                          "finish_reason": "tool_calls"
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var client = CreateClient(httpClient);

        var response = await client.CompleteWithToolsAsync(
            new[] { new Message { Role = "user", Content = "check Haley inventory" } },
            new[]
            {
                new ToolDefinition
                {
                    Name = "inventory",
                    Description = "Checks inventory",
                    Parameters = JsonDocument.Parse("""{"type":"object"}""").RootElement
                }
            },
            CancellationToken.None);

        Assert.AreEqual("I need the inventory tool before answering.", GetStringProperty(response, "ReasoningContent"));
        Assert.AreEqual("{\"summary\":\"tool needed\"}", GetStringProperty(response, "Reasoning"));
        Assert.AreEqual("[{\"type\":\"summary_text\",\"text\":\"tool needed\"}]", GetStringProperty(response, "ReasoningDetails"));
        Assert.AreEqual("[{\"id\":\"rs_1\",\"type\":\"reasoning\"}]", GetStringProperty(response, "CodexReasoningItems"));
        Assert.AreEqual("call_inventory", response.ToolCalls![0].Id);
    }

    [TestMethod]
    public async Task CompleteWithToolsAsync_ReplaysReasoningFieldsOnAssistantToolCallMessages()
    {
        string? capturedJson = null;
        using var httpClient = new HttpClient(new CaptureHandler(request =>
        {
            capturedJson = request.Content!.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            return CreateStopResponse();
        }));
        var client = CreateClient(httpClient);
        var assistantToolMessage = new Message
        {
            Role = "assistant",
            Content = "",
            ToolCalls = new List<ToolCall>
            {
                new() { Id = "call_inventory", Name = "inventory", Arguments = "{\"npc\":\"Haley\"}" }
            }
        };
        SetStringProperty(assistantToolMessage, "ReasoningContent", "I need the inventory tool before answering.");
        SetStringProperty(assistantToolMessage, "Reasoning", "{\"summary\":\"tool needed\"}");
        SetStringProperty(assistantToolMessage, "ReasoningDetails", "[{\"type\":\"summary_text\",\"text\":\"tool needed\"}]");
        SetStringProperty(assistantToolMessage, "CodexReasoningItems", "[{\"id\":\"rs_1\",\"type\":\"reasoning\"}]");

        await client.CompleteWithToolsAsync(
            new[]
            {
                new Message { Role = "user", Content = "check Haley inventory" },
                assistantToolMessage,
                new Message
                {
                    Role = "tool",
                    Content = "{\"ok\":true}",
                    ToolCallId = "call_inventory",
                    ToolName = "inventory"
                }
            },
            Array.Empty<ToolDefinition>(),
            CancellationToken.None);

        Assert.IsNotNull(capturedJson);
        using var payload = JsonDocument.Parse(capturedJson!);
        var replayedAssistant = payload.RootElement.GetProperty("messages")[1];
        Assert.AreEqual(
            "I need the inventory tool before answering.",
            replayedAssistant.GetProperty("reasoning_content").GetString());
        Assert.AreEqual("{\"summary\":\"tool needed\"}", replayedAssistant.GetProperty("reasoning").GetString());
        Assert.AreEqual("[{\"type\":\"summary_text\",\"text\":\"tool needed\"}]", replayedAssistant.GetProperty("reasoning_details").GetString());
        Assert.AreEqual("[{\"id\":\"rs_1\",\"type\":\"reasoning\"}]", replayedAssistant.GetProperty("codex_reasoning_items").GetString());
    }

    [TestMethod]
    public async Task CompleteWithToolsAsync_BadRequestIncludesLocalAndProviderRequestIds()
    {
        using var httpClient = new HttpClient(new CaptureHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"message":"Missing required parameter: messages[2].reasoning_content"}}""",
                    Encoding.UTF8,
                    "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "provider-req-123");
            return response;
        }));
        var client = CreateClient(httpClient);

        var ex = await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            client.CompleteWithToolsAsync(
                new[] { new Message { Role = "user", Content = "hello" } },
                Array.Empty<ToolDefinition>(),
                CancellationToken.None));

        StringAssert.Contains(ex.Message, "llmRequestId=req_llm_");
        StringAssert.Contains(ex.Message, "providerRequestId=provider-req-123");
        StringAssert.Contains(ex.Message, "reasoning_content_error=true");
    }

    private static OpenAiClient CreateClient(HttpClient httpClient)
        => new(
            new LlmConfig
            {
                Provider = "openai",
                Model = "gpt-5.4",
                BaseUrl = "https://proxy.example/v1",
                ApiKey = "test-key"
            },
            httpClient);

    private static HttpResponseMessage CreateStopResponse()
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}]}""",
                Encoding.UTF8,
                "application/json")
        };

    private static string? GetStringProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        Assert.IsNotNull(property, $"{target.GetType().Name} must expose {propertyName}.");
        return property!.GetValue(target) as string;
    }

    private static void SetStringProperty(object target, string propertyName, string value)
    {
        var property = target.GetType().GetProperty(propertyName);
        Assert.IsNotNull(property, $"{target.GetType().Name} must expose {propertyName}.");
        property!.SetValue(target, value);
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
