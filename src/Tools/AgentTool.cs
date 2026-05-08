namespace Hermes.Agent.Tools;

using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;

/// <summary>
/// Tool for spawning subagents with isolated context.
/// </summary>
public sealed class AgentTool : ITool
{
    private readonly IChatClient _chatClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly AgentToolConfig _config;
    private readonly ILogger<AgentTool>? _logger;
    
    public string Name => "agent";
    public string Description => "Spawn a subagent to handle a specialized task with isolated context";
    public Type ParametersType => typeof(AgentParameters);
    
    public AgentTool(
        IChatClient chatClient,
        IToolRegistry toolRegistry,
        AgentToolConfig? config = null,
        ILogger<AgentTool>? logger = null)
    {
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
        _config = config ?? new AgentToolConfig();
        _logger = logger;
    }
    
    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (AgentParameters)parameters;
        
        try
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine($"[Agent: {p.AgentType}] Started");
            output.AppendLine($"Task: {p.Task}");
            output.AppendLine("---");

            _logger?.LogInformation(
                "Starting child agent; childAgentType={ChildAgentType}; depthPolicy={DepthPolicy}; maxSubagentDepth={MaxSubagentDepth}; maxSteps={MaxSteps}",
                p.AgentType,
                "flat-single-child-v1",
                _config.MaxSubagentDepth,
                p.MaxSteps);
            
            // Build agent definition
            var definition = GetAgentDefinition(p.AgentType);
            
            // Build messages
            var messages = new List<Message>
            {
                new() { Role = "user", Content = p.Task }
            };
            
            // Run agent with streaming
            await foreach (var evt in _chatClient.StreamAsync(definition.SystemPrompt, messages, GetToolsForAgent(p.AgentType), ct))
            {
                switch (evt)
                {
                    case StreamEvent.TokenDelta delta:
                        output.Append(delta.Text);
                        break;
                        
                    case StreamEvent.ToolUseStart toolStart:
                        output.AppendLine($"\n[Tool: {toolStart.Name}]");
                        break;
                        
                    case StreamEvent.MessageComplete complete:
                        output.AppendLine($"\n---");
                        output.AppendLine($"[Agent: {p.AgentType}] Completed ({complete.StopReason})");
                        break;
                        
                    case StreamEvent.StreamError error:
                        output.AppendLine($"\n[Error: {error.Error.Message}]");
                        break;
                }
            }
            
            return ToolResult.Ok(output.ToString());
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("Agent execution cancelled");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Agent execution failed: {ex.Message}", ex);
        }
    }
    
    private AgentDefinition GetAgentDefinition(string agentType)
        => _config.ResolveDefinition(agentType);
    
    private IEnumerable<ToolDefinition>? GetToolsForAgent(string agentType)
    {
        var definition = GetAgentDefinition(agentType);
        var tools = new List<ToolDefinition>();
        
        foreach (var toolName in AgentToolConfig.NormalizeToolNames(definition.AllowedTools))
        {
            var tool = _toolRegistry.GetTool(toolName);
            if (tool is not null)
            {
                // Create tool definition from registered tool
                tools.Add(new ToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = GetToolSchema(tool)
                });
            }
        }
        
        return tools.Count > 0 ? tools : null;
    }
    
    private static JsonElement GetToolSchema(ITool tool)
    {
        if (tool is IToolSchemaProvider schemaProvider)
            return schemaProvider.GetParameterSchema();

        // Fallback schema for tools without a custom model-facing contract.
        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });
    }
}

/// <summary>
/// Definition of an agent type.
/// </summary>
public sealed record AgentDefinition(
    string Name,
    string SystemPrompt,
    IReadOnlyList<string> AllowedTools);

/// <summary>
/// Configuration for agent tool.
/// </summary>
public sealed class AgentToolConfig
{
    public static IReadOnlyList<AgentDefinition> BuiltInAgentDefinitions { get; } =
    [
        new(
            "Researcher",
            "You are a research specialist. Synthesize available local context and prior session memory. Be clear about evidence and uncertainty.",
            ["session_search", "skills_list", "skill_view", "memory"]),
        new(
            "Coder",
            "You are an implementation planning specialist. Break work into concrete, verifiable steps using the retained local agent tools.",
            ["session_search", "todo", "memory", "skills_list", "skill_view"]),
        new(
            "Analyst",
            "You are an analysis specialist. Break down complex problems, identify patterns, and provide actionable insights.",
            ["session_search", "memory", "skills_list", "skill_view"]),
        new(
            "Planner",
            "You are a planning specialist. Create detailed, actionable plans with clear steps, dependencies, and success criteria.",
            ["session_search", "todo", "memory"]),
        new(
            "Reviewer",
            "You are a code review specialist. Identify issues, suggest improvements, and ensure code quality and security.",
            ["session_search", "memory", "skills_list", "skill_view"]),
        new(
            "General",
            "You are a helpful assistant. Complete the task efficiently and accurately.",
            ["session_search", "todo", "schedule_cron", "ask_user", "memory", "skills_list", "skill_view", "skill_invoke"])
    ];

    public int MaxSubagentDepth { get; init; } = 3;
    public int MaxTokensPerSubagent { get; init; } = 8000;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    public IReadOnlyList<AgentDefinition> AgentDefinitions { get; init; } = [];

    public AgentDefinition ResolveDefinition(string? agentType)
    {
        var definitions = BuildEffectiveDefinitions();
        var requested = string.IsNullOrWhiteSpace(agentType) ? "general" : agentType.Trim();
        return definitions.TryGetValue(requested, out var definition)
            ? definition
            : definitions["general"];
    }

    public static IReadOnlyList<string> NormalizeToolNames(IEnumerable<string>? toolNames)
        => (toolNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private IReadOnlyDictionary<string, AgentDefinition> BuildEffectiveDefinitions()
    {
        var definitions = new Dictionary<string, AgentDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in BuiltInAgentDefinitions)
            definitions[definition.Name] = NormalizeDefinition(definition);

        foreach (var definition in AgentDefinitions ?? [])
        {
            if (string.IsNullOrWhiteSpace(definition.Name))
                continue;

            definitions[definition.Name.Trim()] = NormalizeDefinition(definition);
        }

        return definitions;
    }

    private static AgentDefinition NormalizeDefinition(AgentDefinition definition)
        => definition with
        {
            Name = definition.Name.Trim(),
            AllowedTools = NormalizeToolNames(definition.AllowedTools)
        };
}

public sealed class AgentParameters
{
    public required string AgentType { get; init; }
    public required string Task { get; init; }
    public string? Context { get; init; }
    public int MaxSteps { get; init; } = 10;
}

/// <summary>
/// Tool registry interface for discovering available tools.
/// </summary>
public interface IToolRegistry
{
    ITool? GetTool(string name);
    IEnumerable<ITool> GetAllTools();
    void RegisterTool(ITool tool);
}

/// <summary>
/// Default tool registry implementation.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    
    public ITool? GetTool(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;
    
    public IEnumerable<ITool> GetAllTools() => _tools.Values;
    
    public void RegisterTool(ITool tool) => _tools[tool.Name] = tool;
}
