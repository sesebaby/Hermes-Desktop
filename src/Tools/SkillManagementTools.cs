namespace Hermes.Agent.Tools;

using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Hermes.Agent.Skills;

/// <summary>
/// Python-compatible skill inventory tool.
/// Reference: external/hermes-agent-main/tools/skills_tool.py SKILLS_LIST_SCHEMA.
/// </summary>
public sealed class SkillsListTool : ITool, IToolSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = SkillToolJson.Options;
    private readonly SkillManager _skillManager;

    public string Name => "skills_list";
    public string Description => "List available skills (name + description). Use skill_view(name) to load full content.";
    public Type ParametersType => typeof(SkillsListParameters);

    public SkillsListTool(SkillManager skillManager)
    {
        _skillManager = skillManager;
    }

    public JsonElement GetParameterSchema()
        => SkillToolJson.Schema(new Dictionary<string, object?>
        {
            ["category"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Optional category filter to narrow results"
            }
        }, Array.Empty<string>());

    public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (SkillsListParameters)parameters;
        var skills = _skillManager.ListSkills()
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
            .Select(skill => new
            {
                name = skill.Name,
                description = skill.Description,
                category = _skillManager.GetCategory(skill),
                path = _skillManager.GetRelativeSkillPath(skill)
            })
            .Where(skill => string.IsNullOrWhiteSpace(p.Category) ||
                            string.Equals(skill.category, p.Category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(skill => skill.category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(skill => skill.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categories = skills
            .Select(skill => skill.category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new
        {
            success = true,
            skills,
            categories,
            count = skills.Count,
            hint = "Use skill_view(name) to see full content, tags, and linked files"
        };

        return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(result, JsonOptions)));
    }
}

/// <summary>
/// Python-compatible skill content loader.
/// Reference: external/hermes-agent-main/tools/skills_tool.py SKILL_VIEW_SCHEMA.
/// </summary>
public sealed class SkillViewTool : ITool, IToolSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = SkillToolJson.Options;
    private readonly SkillManager _skillManager;

    public string Name => "skill_view";
    public string Description => "Skills allow for loading information about specific tasks and workflows, as well as scripts and templates. Load a skill's full content or access its linked files (references, templates, scripts). First call returns SKILL.md content plus a 'linked_files' dict showing available references/templates/scripts. To access those, call again with file_path parameter.";
    public Type ParametersType => typeof(SkillViewParameters);

    public SkillViewTool(SkillManager skillManager)
    {
        _skillManager = skillManager;
    }

    public JsonElement GetParameterSchema()
        => SkillToolJson.Schema(new Dictionary<string, object?>
        {
            ["name"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "The skill name (use skills_list to see available skills)."
            },
            ["file_path"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "OPTIONAL: Path to a linked file within the skill (e.g., 'references/api.md', 'templates/config.yaml', 'scripts/validate.py'). Omit to get the main SKILL.md content."
            }
        }, new[] { "name" });

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (SkillViewParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Name))
            return ToolResult.Fail(SkillToolJson.Error("name is required."));

        try
        {
            if (!string.IsNullOrWhiteSpace(p.FilePath))
            {
                var fileContent = await _skillManager.ReadSkillFileViewAsync(p.Name, p.FilePath, ct);
                var fileResult = new
                {
                    success = true,
                    name = p.Name,
                    file = fileContent.File,
                    content = fileContent.Content,
                    file_type = fileContent.FileType,
                    is_binary = fileContent.IsBinary
                };
                return ToolResult.Ok(JsonSerializer.Serialize(fileResult, JsonOptions));
            }

            var skill = _skillManager.GetSkill(p.Name);
            if (skill is null)
                return ToolResult.Fail(SkillToolJson.Error($"Skill '{p.Name}' not found.", new { available_skills = _skillManager.ListSkills().Select(s => s.Name).Take(20).ToList() }));

            var content = await _skillManager.ReadSkillContentAsync(p.Name, ct);
            var linkedFiles = BuildLinkedFiles(_skillManager.ListSupportingFiles(p.Name));
            var metadata = _skillManager.ReadSkillViewMetadata(p.Name);
            var result = new
            {
                success = true,
                name = skill.Name,
                description = skill.Description,
                tags = metadata.Tags,
                related_skills = metadata.RelatedSkills,
                content,
                path = _skillManager.GetRelativeSkillPath(skill),
                skill_dir = _skillManager.GetSkillDirectory(skill),
                linked_files = linkedFiles.Count == 0 ? null : linkedFiles,
                usage_hint = linkedFiles.Count == 0
                    ? null
                    : "To view linked files, call skill_view(name, file_path) where file_path is e.g. 'references/api.md' or 'assets/config.yaml'",
                required_environment_variables = metadata.RequiredEnvironmentVariables,
                required_commands = Array.Empty<string>(),
                missing_required_environment_variables = Array.Empty<string>(),
                missing_credential_files = metadata.RequiredCredentialFiles
                    .Where(path => !File.Exists(Path.Combine(_skillManager.GetSkillDirectory(skill), path)))
                    .ToList(),
                missing_required_commands = Array.Empty<string>(),
                setup_needed = metadata.RequiredCredentialFiles
                    .Any(path => !File.Exists(Path.Combine(_skillManager.GetSkillDirectory(skill), path))),
                setup_skipped = false,
                readiness_status = metadata.RequiredCredentialFiles
                    .Any(path => !File.Exists(Path.Combine(_skillManager.GetSkillDirectory(skill), path)))
                    ? "setup_needed"
                    : "available",
                metadata = metadata.Metadata.Count == 0 ? null : metadata.Metadata
            };

            return ToolResult.Ok(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (FileNotFoundException ex)
        {
            var available = string.IsNullOrWhiteSpace(p.Name)
                ? new Dictionary<string, List<string>>()
                : BuildLinkedFiles(_skillManager.ListSkillFiles(p.Name));
            return ToolResult.Fail(SkillToolJson.Error(ex.Message, new
            {
                available_files = available.Count == 0 ? null : available,
                hint = "Use one of the available file paths listed above"
            }));
        }
        catch (Exception ex) when (ex is SkillNotFoundException or ArgumentException)
        {
            return ToolResult.Fail(SkillToolJson.Error(ex.Message));
        }
    }

    private static Dictionary<string, List<string>> BuildLinkedFiles(IReadOnlyList<string> files)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var group = file.Split('/', 2)[0];
            if (!result.TryGetValue(group, out var grouped))
            {
                grouped = new List<string>();
                result[group] = grouped;
            }
            grouped.Add(file);
        }

        foreach (var key in result.Keys.ToList())
            result[key] = result[key].Order(StringComparer.OrdinalIgnoreCase).ToList();

        return result;
    }
}

/// <summary>
/// Python-compatible procedural-memory mutation tool.
/// Reference: external/hermes-agent-main/tools/skill_manager_tool.py SKILL_MANAGE_SCHEMA.
/// </summary>
public sealed class SkillManageTool : ITool, IToolSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = SkillToolJson.Options;
    private readonly SkillManager _skillManager;

    public string Name => "skill_manage";
    public string Description =>
        "Manage skills (create, update, delete). Skills are your procedural memory -- reusable approaches for recurring task types.\n\n" +
        "Actions: create (full SKILL.md + optional category), patch (old_string/new_string -- preferred for fixes), edit (full SKILL.md rewrite -- major overhauls only), delete, write_file, remove_file.\n\n" +
        "Create when: complex task succeeded (5+ calls), errors overcome, user-corrected approach worked, non-trivial workflow discovered, or user asks you to remember a procedure.\n" +
        "Update when: instructions stale/wrong, OS-specific failures, missing steps or pitfalls found during use. If you used a skill and hit issues not covered by it, patch it immediately.\n\n" +
        "After difficult/iterative tasks, offer to save as a skill. Skip for simple one-offs. Confirm with user before creating/deleting.\n\n" +
        "Good skills: trigger conditions, numbered steps with exact commands, pitfalls section, verification steps. Use skill_view() to see format examples.";

    public Type ParametersType => typeof(SkillManageParameters);

    public SkillManageTool(SkillManager skillManager)
    {
        _skillManager = skillManager;
    }

    public JsonElement GetParameterSchema()
        => SkillToolJson.Schema(new Dictionary<string, object?>
        {
            ["action"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["enum"] = new[] { "create", "patch", "edit", "delete", "write_file", "remove_file" },
                ["description"] = "The action to perform."
            },
            ["name"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Skill name (lowercase, hyphens/underscores, max 64 chars). Must match an existing skill for patch/edit/delete/write_file/remove_file."
            },
            ["content"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Full SKILL.md content (YAML frontmatter + markdown body). Required for 'create' and 'edit'. For 'edit', read the skill first with skill_view() and provide the complete updated text."
            },
            ["old_string"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Text to find in the file (required for 'patch'). Must be unique unless replace_all=true. Include enough surrounding context to ensure uniqueness."
            },
            ["new_string"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Replacement text (required for 'patch'). Can be empty string to delete the matched text."
            },
            ["replace_all"] = new Dictionary<string, object?>
            {
                ["type"] = "boolean",
                ["description"] = "For 'patch': replace all occurrences instead of requiring a unique match (default: false)."
            },
            ["category"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Optional category/domain for organizing the skill (e.g., 'devops', 'data-science', 'mlops'). Creates a subdirectory grouping. Only used with 'create'."
            },
            ["file_path"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Path to a supporting file within the skill directory. For 'write_file'/'remove_file': required, must be under references/, templates/, scripts/, or assets/. For 'patch': optional, defaults to SKILL.md if omitted."
            },
            ["file_content"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Content for the file. Required for 'write_file'."
            }
        }, new[] { "action", "name" });

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (SkillManageParameters)parameters;
        var action = p.Action?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(p.Name))
            return ToolResult.Fail(SkillToolJson.Error("name is required."));

        try
        {
            return action switch
            {
                "create" => await CreateAsync(p, ct),
                "edit" => await EditAsync(p, ct),
                "patch" => await PatchAsync(p, ct),
                "delete" => await DeleteAsync(p, ct),
                "write_file" => await WriteFileAsync(p, ct),
                "remove_file" => RemoveFile(p),
                _ => ToolResult.Fail(SkillToolJson.Error($"Unknown action '{p.Action}'. Use: create, edit, patch, delete, write_file, remove_file"))
            };
        }
        catch (SkillPatchException ex)
        {
            return ToolResult.Fail(SkillToolJson.Error(ex.Message, new { file_preview = ex.FilePreview }));
        }
        catch (Exception ex) when (ex is SkillNotFoundException or FileNotFoundException or ArgumentException or InvalidOperationException)
        {
            return ToolResult.Fail(SkillToolJson.Error(ex.Message));
        }
    }

    private async Task<ToolResult> CreateAsync(SkillManageParameters p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Content))
            return ToolResult.Fail(SkillToolJson.Error("content is required for 'create'. Provide the full SKILL.md text (frontmatter + body)."));

        var skill = await _skillManager.CreateSkillFromContentAsync(p.Name, p.Content, p.Category, ct);
        return Ok(new
        {
            message = $"Skill '{skill.Name}' created.",
            path = _skillManager.GetRelativeSkillDirectory(skill),
            skill_md = skill.FilePath,
            category = _skillManager.GetCategory(skill),
            hint = $"To add reference files, templates, or scripts, use skill_manage(action='write_file', name='{skill.Name}', file_path='references/example.md', file_content='...')"
        });
    }

    private async Task<ToolResult> EditAsync(SkillManageParameters p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.Content))
            return ToolResult.Fail(SkillToolJson.Error("content is required for 'edit'. Provide the full updated SKILL.md text."));

        var skill = await _skillManager.EditSkillAsync(p.Name, p.Content, ct);
        return Ok(new { message = $"Skill '{skill.Name}' updated.", path = _skillManager.GetSkillDirectory(skill) });
    }

    private async Task<ToolResult> PatchAsync(SkillManageParameters p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.OldString))
            return ToolResult.Fail(SkillToolJson.Error("old_string is required for 'patch'. Provide the text to find."));
        if (p.NewString is null)
            return ToolResult.Fail(SkillToolJson.Error("new_string is required for 'patch'. Use empty string to delete matched text."));

        var result = await _skillManager.PatchSkillAsync(p.Name, p.OldString, p.NewString, p.FilePath, p.ReplaceAll, ct);
        return Ok(new
        {
            message = $"Patched {result.TargetLabel} in skill '{p.Name}' ({result.MatchCount} replacement{(result.MatchCount == 1 ? "" : "s")}).",
            match_count = result.MatchCount,
            strategy = result.Strategy
        });
    }

    private async Task<ToolResult> DeleteAsync(SkillManageParameters p, CancellationToken ct)
    {
        await _skillManager.DeleteSkillAsync(p.Name, ct);
        return Ok(new { message = $"Skill '{p.Name}' deleted." });
    }

    private async Task<ToolResult> WriteFileAsync(SkillManageParameters p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.FilePath))
            return ToolResult.Fail(SkillToolJson.Error("file_path is required for 'write_file'. Example: 'references/api-guide.md'"));
        if (p.FileContent is null)
            return ToolResult.Fail(SkillToolJson.Error("file_content is required for 'write_file'."));

        var path = await _skillManager.WriteSupportingFileAsync(p.Name, p.FilePath, p.FileContent, ct);
        return Ok(new { message = $"File '{p.FilePath}' written to skill '{p.Name}'.", path });
    }

    private ToolResult RemoveFile(SkillManageParameters p)
    {
        if (string.IsNullOrWhiteSpace(p.FilePath))
            return ToolResult.Fail(SkillToolJson.Error("file_path is required for 'remove_file'."));

        _skillManager.RemoveSupportingFile(p.Name, p.FilePath);
        return Ok(new { message = $"File '{p.FilePath}' removed from skill '{p.Name}'." });
    }

    private static ToolResult Ok(object payload)
    {
        var dict = new Dictionary<string, object?> { ["success"] = true };
        foreach (var property in payload.GetType().GetProperties())
            dict[property.Name.ToSnakeCase()] = property.GetValue(payload);
        return ToolResult.Ok(JsonSerializer.Serialize(dict, JsonOptions));
    }
}

public sealed class SkillsListParameters
{
    [Description("Optional category filter to narrow results.")]
    public string? Category { get; init; }
}

public sealed class SkillViewParameters
{
    [Description("The skill name.")]
    public required string Name { get; init; }

    [JsonPropertyName("file_path")]
    [Description("Optional path to a linked file within the skill.")]
    public string? FilePath { get; init; }
}

public sealed class SkillManageParameters
{
    [Description("The action to perform.")]
    public required string Action { get; init; }

    [Description("Skill name.")]
    public required string Name { get; init; }

    [Description("Full SKILL.md content for create/edit.")]
    public string? Content { get; init; }

    [Description("Optional category/domain for organizing a new skill.")]
    public string? Category { get; init; }

    [JsonPropertyName("file_path")]
    [Description("Path to a supporting file within the skill directory.")]
    public string? FilePath { get; init; }

    [JsonPropertyName("file_content")]
    [Description("Content for write_file.")]
    public string? FileContent { get; init; }

    [JsonPropertyName("old_string")]
    [Description("Text to find for patch.")]
    public string? OldString { get; init; }

    [JsonPropertyName("new_string")]
    [Description("Replacement text for patch.")]
    public string? NewString { get; init; }

    [JsonPropertyName("replace_all")]
    [Description("For patch: replace all occurrences.")]
    public bool ReplaceAll { get; init; }
}

internal static class SkillToolJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static JsonElement Schema(Dictionary<string, object?> properties, IReadOnlyList<string> required)
    {
        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };

        return JsonSerializer.SerializeToElement(schema, Options);
    }

    public static string Error(string error, object? extra = null)
    {
        var dict = new Dictionary<string, object?> { ["success"] = false, ["error"] = error };
        if (extra is not null)
        {
            foreach (var property in extra.GetType().GetProperties())
                dict[property.Name.ToSnakeCase()] = property.GetValue(extra);
        }
        return JsonSerializer.Serialize(dict, Options);
    }
}

internal static class SkillToolStringExtensions
{
    public static string ToSnakeCase(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var chars = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    chars.Add('_');
                chars.Add(char.ToLowerInvariant(c));
            }
            else
            {
                chars.Add(c);
            }
        }

        return new string(chars.ToArray());
    }
}
