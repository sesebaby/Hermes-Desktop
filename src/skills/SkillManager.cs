namespace Hermes.Agent.Skills;

using Hermes.Agent.LLM;
using Hermes.Agent.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

/// <summary>
/// Skills System - Markdown-based custom capabilities.
/// Skills are directories containing a SKILL.md file with YAML frontmatter.
/// </summary>
public sealed class SkillManager
{
    private readonly string _skillsDir;
    private readonly ILogger<SkillManager> _logger;
    private readonly ConcurrentDictionary<string, Skill> _skills = new();
    private static readonly HashSet<string> AllowedSupportingSubdirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "references", "templates", "scripts", "assets"
    };
    
    public SkillManager(string skillsDir, ILogger<SkillManager> logger)
    {
        _skillsDir = skillsDir;
        _logger = logger;
        
        Directory.CreateDirectory(skillsDir);
        LoadSkills();
    }

    /// <summary>
    /// Load all skills from disk.
    /// </summary>
    private void LoadSkills()
    {
        if (!Directory.Exists(_skillsDir))
            return;
        
        var skillFiles = Directory.EnumerateFiles(_skillsDir, "SKILL.md", SearchOption.AllDirectories);
        
        foreach (var file in skillFiles)
        {
            try
            {
                var skill = ParseSkillFile(file);
                if (skill != null)
                {
                    _skills[skill.Name] = skill;
                    _logger.LogInformation("Loaded skill: {Name} from {Path}", skill.Name, file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load skill from {File}", file);
            }
        }
    }
    
    /// <summary>
    /// Parse skill from markdown file with YAML frontmatter.
    /// </summary>
    private Skill? ParseSkillFile(string path)
    {
        var content = File.ReadAllText(path);
        
        // Parse YAML frontmatter
        if (!content.StartsWith("---"))
            return null;
        
        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex == -1)
            return null;
        
        var yamlContent = content.Substring(3, endIndex - 3).Trim();
        var body = content.Substring(endIndex + 3).Trim();
        
        var frontmatter = ParseYamlFrontmatter(yamlContent);
        if (string.IsNullOrWhiteSpace(frontmatter.Name))
            return null;
        
        return new Skill
        {
            Name = frontmatter.Name,
            Description = frontmatter.Description,
            FilePath = path,
            Tools = frontmatter.Tools?.Split(',').Select(t => t.Trim()).ToList() ?? new List<string>(),
            Model = frontmatter.Model,
            SystemPrompt = body
        };
    }
    
    private SkillFrontmatter ParseYamlFrontmatter(string yaml)
    {
        var frontmatter = new SkillFrontmatter();
        
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;
            
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex == -1)
                continue;
            
            var key = trimmed.Substring(0, colonIndex).Trim();
            var value = trimmed.Substring(colonIndex + 1).Trim().Trim('"', '\'');
            
            switch (key.ToLower())
            {
                case "name":
                    frontmatter.Name = value;
                    break;
                case "description":
                    frontmatter.Description = value;
                    break;
                case "tools":
                    frontmatter.Tools = value;
                    break;
                case "model":
                    frontmatter.Model = value;
                    break;
            }
        }
        
        return frontmatter;
    }
    
    /// <summary>
    /// Get skill by name.
    /// </summary>
    public Skill? GetSkill(string name)
    {
        return _skills.TryGetValue(name, out var skill) ? skill : null;
    }
    
    /// <summary>
    /// List all skills.
    /// </summary>
    public List<Skill> ListSkills()
    {
        return _skills.Values.ToList();
    }

    public string GetSkillDirectory(Skill skill)
        => GetSkillRootDirectory(skill);

    public string GetRelativeSkillPath(Skill skill)
        => Path.GetRelativePath(_skillsDir, skill.FilePath).Replace('\\', '/');

    public string GetRelativeSkillDirectory(Skill skill)
        => Path.GetRelativePath(_skillsDir, GetSkillRootDirectory(skill)).Replace('\\', '/');

    public string? GetCategory(Skill skill)
    {
        var relativeDirectory = GetRelativeSkillDirectory(skill);
        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
            return null;

        var parts = relativeDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join('/', parts[..^1]) : null;
    }

    public string BuildSkillsMandatoryPrompt()
    {
        var skills = ListSkills()
            .Select(skill => new
            {
                Skill = skill,
                Category = GetCategory(skill) ?? "",
                skill.Name,
                skill.Description
            })
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (skills.Count == 0)
            return "";

        var indexLines = new List<string>();
        foreach (var categoryGroup in skills.GroupBy(item => item.Category))
        {
            var category = categoryGroup.Key;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var description = ReadCategoryDescription(category);
                indexLines.Add(string.IsNullOrWhiteSpace(description)
                    ? $"  {category}:"
                    : $"  {category}: {description}");
            }
            else
            {
                indexLines.Add("  uncategorized:");
            }

            foreach (var item in categoryGroup)
            {
                indexLines.Add(string.IsNullOrWhiteSpace(item.Description)
                    ? $"    - {item.Name}"
                    : $"    - {item.Name}: {item.Description}");
            }
        }

        return
            "## Skills (mandatory)\n" +
            "Before replying, scan the skills below. If a skill matches or is even partially relevant " +
            "to your task, you MUST load it with skill_view(name) and follow its instructions. " +
            "Err on the side of loading -- it is always better to have context you don't need " +
            "than to miss critical steps, pitfalls, or established workflows. " +
            "Skills contain specialized knowledge -- API endpoints, tool-specific commands, " +
            "and proven workflows that outperform general-purpose approaches. Load the skill " +
            "even if you think you could handle the task with basic built-in capabilities. " +
            "Skills also encode the user's preferred approach, conventions, and quality standards " +
            "for tasks like code review, planning, and testing -- load them even for tasks you " +
            "already know how to do, because the skill defines how it should be done here.\n" +
            "Whenever the user asks you to configure, set up, install, enable, disable, modify, " +
            "or troubleshoot Hermes Agent itself -- its CLI, config, models, providers, tools, " +
            "skills, voice, gateway, plugins, or any feature -- load the `hermes-agent` skill " +
            "first. It has the actual commands so you don't have to guess or invent workarounds.\n" +
            "If a skill has issues, fix it with skill_manage(action='patch').\n" +
            "After difficult/iterative tasks, offer to save as a skill. " +
            "If a skill you loaded was missing steps, had wrong commands, or needed " +
            "pitfalls you discovered, update it before finishing.\n\n" +
            "<available_skills>\n" +
            string.Join("\n", indexLines) +
            "\n</available_skills>\n\n" +
            "Only proceed without loading a skill if genuinely none are relevant to the task.";
    }

    public async Task<string> ReadSkillContentAsync(string name, CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        return await File.ReadAllTextAsync(skill.FilePath, ct);
    }

    public async Task<string> ReadSkillFileAsync(string name, string? filePath, CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        var target = string.IsNullOrWhiteSpace(filePath)
            ? skill.FilePath
            : ResolveSkillFilePath(skill, filePath, requireSupportingSubdir: false);

        if (!File.Exists(target))
            throw new FileNotFoundException($"File not found in skill '{name}': {filePath ?? "SKILL.md"}", target);

        return await File.ReadAllTextAsync(target, ct);
    }

    public async Task<SkillFileView> ReadSkillFileViewAsync(string name, string filePath, CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        var target = ResolveSkillFilePath(skill, filePath, requireSupportingSubdir: false);
        if (!File.Exists(target))
            throw new FileNotFoundException($"File not found in skill '{name}': {filePath}", target);

        var bytes = await File.ReadAllBytesAsync(target, ct);
        if (LooksBinary(bytes))
        {
            return new SkillFileView(
                filePath,
                $"[Binary file: {Path.GetFileName(target)}, size: {bytes.Length} bytes]",
                Path.GetExtension(target),
                IsBinary: true);
        }

        return new SkillFileView(
            filePath,
            System.Text.Encoding.UTF8.GetString(bytes),
            Path.GetExtension(target),
            IsBinary: false);
    }

    public SkillViewMetadata ReadSkillViewMetadata(string name)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        var content = File.ReadAllText(skill.FilePath);
        var frontmatter = ExtractFrontmatter(content);
        return ParseSkillViewMetadata(frontmatter);
    }

    public IReadOnlyList<string> ListSupportingFiles(string name)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        var root = GetSkillRootDirectory(skill);
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        var files = new List<string>();
        foreach (var subdir in AllowedSupportingSubdirs.Order(StringComparer.OrdinalIgnoreCase))
        {
            var dir = Path.Combine(root, subdir);
            if (!Directory.Exists(dir))
                continue;

            files.AddRange(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Order(StringComparer.OrdinalIgnoreCase));
        }

        return files;
    }

    public IReadOnlyList<string> ListSkillFiles(string name)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        var root = GetSkillRootDirectory(skill);
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Skill> CreateSkillFromContentAsync(
        string name,
        string content,
        string? category,
        CancellationToken ct)
    {
        var safeName = ValidateSkillName(name);
        ValidateCategory(category);
        ValidateSkillContent(content);
        ValidateSkillFrontmatterName(content, safeName);

        if (_skills.ContainsKey(safeName))
            throw new ArgumentException($"A skill named '{safeName}' already exists.");

        var dir = ResolveNewSkillDirectory(safeName, category);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "SKILL.md");

        await AtomicWriteTextAsync(path, content, ct);

        try
        {
            if (Hermes.Agent.Security.SecretScanner.ContainsSecrets(content))
            {
                Directory.Delete(dir, recursive: true);
                throw new InvalidOperationException("Skill content contains secrets -- creation blocked and rolled back.");
            }

            var skill = ParseSkillFile(path) ?? throw new InvalidOperationException("Created skill could not be parsed.");
            _skills[skill.Name] = skill;
            _logger.LogInformation("Created skill: {Name} at {Path}", skill.Name, path);
            return skill;
        }
        catch
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            throw;
        }
    }
    
    /// <summary>
    /// Invoke a skill.
    /// Returns the system prompt to inject.
    /// </summary>
    public async Task<string> InvokeSkillAsync(string skillName, string userQuery, CancellationToken ct)
    {
        if (!_skills.TryGetValue(skillName, out var skill))
        {
            throw new SkillNotFoundException(skillName);
        }
        
        _logger.LogInformation("Invoking skill: {Name}", skillName);
        
        // Build skill context
        var context = $@"
## Active Skill: {skill.Name}
{skill.Description}

### Tools Available
{string.Join(", ", skill.Tools)}

### Instructions
{skill.SystemPrompt}

### User Query
{userQuery}
";
        
        return context;
    }
    
    // ── Validation constants (upstream: skill_manager_tool.py) ──
    private static readonly System.Text.RegularExpressions.Regex NamePattern =
        new(@"^[a-z0-9][a-z0-9._-]*$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private const int MaxNameLength = 64;
    private const int MaxDescriptionLength = 1024;
    private const int MaxContentLength = 100_000;

    /// <summary>
    /// Create a new skill with validation, atomic write, and security scanning.
    /// Upstream ref: tools/skill_manager_tool.py _create_skill
    /// </summary>
    public async Task<Skill> CreateSkillAsync(
        string name,
        string description,
        string systemPrompt,
        List<string> tools,
        string? model,
        string? category,
        CancellationToken ct)
    {
        // ── Validation (upstream patterns) ──
        var safeName = name.ToLowerInvariant().Replace(" ", "-");
        if (safeName.Length > MaxNameLength)
            throw new ArgumentException($"Skill name too long (max {MaxNameLength} chars)");
        if (!NamePattern.IsMatch(safeName))
            throw new ArgumentException($"Invalid skill name: must match {NamePattern}");
        if (description.Length > MaxDescriptionLength)
            throw new ArgumentException($"Description too long (max {MaxDescriptionLength} chars)");
        if (_skills.ContainsKey(safeName))
            throw new ArgumentException($"Skill '{safeName}' already exists");

        // Build content
        var frontmatter = $"---\nname: {safeName}\ndescription: {description}\ntools: {string.Join(", ", tools)}\n";
        if (model is not null) frontmatter += $"model: {model}\n";
        frontmatter += "---\n";
        var content = frontmatter + systemPrompt;

        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Skill content too long (max {MaxContentLength} chars)");

        // Determine path (with optional category subdirectory)
        var dir = category is not null ? Path.Combine(_skillsDir, category) : _skillsDir;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{safeName}.md");

        // ── Atomic write (upstream pattern: temp file + rename) ──
        var tempPath = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create skill {Name}", safeName);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        // ── Security scan + rollback ──
        if (Hermes.Agent.Security.SecretScanner.ContainsSecrets(content))
        {
            File.Delete(path);
            throw new InvalidOperationException("Skill content contains secrets — creation blocked and rolled back.");
        }

        var skill = new Skill
        {
            Name = safeName,
            Description = description,
            FilePath = path,
            Tools = tools,
            Model = model,
            SystemPrompt = systemPrompt
        };

        _skills[safeName] = skill;
        _logger.LogInformation("Created skill: {Name} at {Path}", safeName, path);
        return skill;
    }

    /// <summary>
    /// Edit (full rewrite) an existing skill with validation and rollback.
    /// Upstream ref: tools/skill_manager_tool.py _edit_skill
    /// </summary>
    public async Task<Skill> EditSkillAsync(string name, string newContent, CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var existing))
            throw new SkillNotFoundException(name);
        ValidateSkillContent(newContent);
        ValidateSkillFrontmatterName(newContent, name);
        if (newContent.Length > MaxContentLength)
            throw new ArgumentException($"Skill content too long (max {MaxContentLength} chars)");

        // Backup original for rollback
        var backup = await File.ReadAllTextAsync(existing.FilePath, ct);

        // Atomic write
        var tempPath = existing.FilePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, newContent, ct);
            File.Move(tempPath, existing.FilePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to edit skill {Name}", name);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        // Security scan + rollback
        if (Hermes.Agent.Security.SecretScanner.ContainsSecrets(newContent))
        {
            await File.WriteAllTextAsync(existing.FilePath, backup, ct);
            throw new InvalidOperationException("Edited skill contains secrets — rolled back to original.");
        }

        // Re-parse
        var updated = ParseSkillFile(existing.FilePath);
        if (updated is not null)
        {
            _skills.TryRemove(name, out _);
            _skills[updated.Name] = updated;
        }

        _logger.LogInformation("Edited skill: {Name}", name);
        return updated ?? existing;
    }

    /// <summary>
    /// Patch a skill with targeted find-and-replace.
    /// Upstream ref: tools/skill_manager_tool.py _patch_skill
    /// </summary>
    public async Task<Skill> PatchSkillAsync(string name, string oldText, string newText, bool replaceAll, CancellationToken ct)
        => (await PatchSkillAsync(name, oldText, newText, filePath: null, replaceAll, ct)).Skill;

    public async Task<SkillPatchResult> PatchSkillAsync(
        string name,
        string oldText,
        string newText,
        string? filePath,
        bool replaceAll,
        CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var existing))
            throw new SkillNotFoundException(name);

        var target = string.IsNullOrWhiteSpace(filePath)
            ? existing.FilePath
            : ResolveSkillFilePath(existing, filePath, requireSupportingSubdir: true);
        if (!File.Exists(target))
            throw new FileNotFoundException($"File not found in skill '{name}': {filePath ?? "SKILL.md"}", target);

        var content = await File.ReadAllTextAsync(target, ct);
        var backup = content;

        var replacement = FuzzyTextReplacer.Replace(content, oldText, newText, replaceAll);
        if (!replacement.Success)
        {
            var preview = content[..Math.Min(content.Length, 500)];
            var hint = FuzzyTextReplacer.FormatNoMatchHint(replacement.Error ?? "", oldText, content);
            throw new SkillPatchException(
                $"{replacement.Error}{hint}",
                preview);
        }

        content = replacement.Content;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                ValidateSkillContent(content);
            }
            catch (ArgumentException ex)
            {
                throw new SkillPatchException($"Patch would break SKILL.md structure: {ex.Message}", content[..Math.Min(content.Length, 500)]);
            }
        }

        // Atomic write
        var tempPath = target + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, target, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to patch skill {Name}", name);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        // Security scan + rollback
        if (Hermes.Agent.Security.SecretScanner.ContainsSecrets(content))
        {
            await File.WriteAllTextAsync(target, backup, ct);
            throw new InvalidOperationException("Patched skill contains secrets — rolled back.");
        }

        var updated = string.IsNullOrWhiteSpace(filePath) ? ParseSkillFile(existing.FilePath) : existing;
        if (updated is not null) _skills[updated.Name] = updated;

        _logger.LogInformation("Patched skill: {Name}", name);
        return new SkillPatchResult(
            updated ?? existing,
            replacement.MatchCount,
            replacement.Strategy ?? "unknown",
            string.IsNullOrWhiteSpace(filePath) ? "SKILL.md" : filePath);
    }
    
    /// <summary>
    /// Delete a skill.
    /// </summary>
    public async Task DeleteSkillAsync(string name, CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var skill))
        {
            throw new SkillNotFoundException(name);
        }

        var root = GetSkillRootDirectory(skill);
        if (Path.GetFileName(skill.FilePath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
            TryDeleteEmptyCategoryDirectory(root);
        }
        else if (File.Exists(skill.FilePath))
        {
            File.Delete(skill.FilePath);
        }
        
        _skills.TryRemove(name, out _);
        
        _logger.LogInformation("Deleted skill: {Name}", name);
    }

    public async Task<string> WriteSupportingFileAsync(string name, string filePath, string fileContent, CancellationToken ct)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);
        ValidateSupportingFileContent(fileContent);

        var target = ResolveSkillFilePath(skill, filePath, requireSupportingSubdir: true);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await AtomicWriteTextAsync(target, fileContent, ct);

        if (Hermes.Agent.Security.SecretScanner.ContainsSecrets(fileContent))
        {
            File.Delete(target);
            throw new InvalidOperationException("Supporting file contains secrets -- write blocked and rolled back.");
        }

        return target;
    }

    public void RemoveSupportingFile(string name, string filePath)
    {
        if (!_skills.TryGetValue(name, out var skill))
            throw new SkillNotFoundException(name);

        var target = ResolveSkillFilePath(skill, filePath, requireSupportingSubdir: true);
        if (!File.Exists(target))
            throw new FileNotFoundException($"File '{filePath}' not found in skill '{name}'.", target);

        File.Delete(target);
        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);
    }

    private string ResolveNewSkillDirectory(string safeName, string? category)
    {
        var baseDir = string.IsNullOrWhiteSpace(category) ? _skillsDir : Path.Combine(_skillsDir, category);
        var fullBase = ResolvePathFollowingLinks(baseDir);
        var fullRoot = ResolvePathFollowingLinks(_skillsDir);
        if (!IsPathWithinDirectory(fullBase, fullRoot))
            throw new ArgumentException("Skill category resolves outside the skills directory.");

        return Path.Combine(fullBase, safeName);
    }

    private string? ReadCategoryDescription(string category)
    {
        var path = Path.Combine(_skillsDir, category, "DESCRIPTION.md");
        if (!File.Exists(path))
            return null;

        try
        {
            var content = File.ReadAllText(path);
            var body = content;
            if (content.StartsWith("---", StringComparison.Ordinal))
            {
                var end = content.IndexOf("---", 3, StringComparison.Ordinal);
                if (end >= 0)
                    body = content[(end + 3)..];
            }

            var description = body
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal));
            return description is null || description.Length <= MaxDescriptionLength
                ? description
                : description[..(MaxDescriptionLength - 3)] + "...";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read skill category description for {Category}", category);
            return null;
        }
    }

    private static void ValidateCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;

        if (category.Contains("..", StringComparison.Ordinal) ||
            category.Contains('\\') ||
            category.Contains('/') ||
            Path.IsPathRooted(category) ||
            !NamePattern.IsMatch(category))
        {
            throw new ArgumentException("Invalid category. Use a filesystem-safe single directory name.");
        }
    }

    private static string ValidateSkillName(string name)
    {
        var safeName = name.Trim();
        if (safeName.Length == 0)
            throw new ArgumentException("Skill name is required.");
        if (safeName.Length > MaxNameLength)
            throw new ArgumentException($"Skill name too long (max {MaxNameLength} chars)");
        if (!NamePattern.IsMatch(safeName))
            throw new ArgumentException($"Invalid skill name: must match {NamePattern}");
        return safeName;
    }

    private static void ValidateSkillContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Skill content is required.");
        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Skill content too long (max {MaxContentLength} chars)");
        if (!content.StartsWith("---", StringComparison.Ordinal))
            throw new ArgumentException("Skill content must start with YAML frontmatter.");
        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            throw new ArgumentException("Skill content must include closing YAML frontmatter.");
        var yaml = content.Substring(3, endIndex - 3);
        if (!yaml.Contains("name:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Skill frontmatter must include name.");
        if (!yaml.Contains("description:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Skill frontmatter must include description.");
    }

    private static string ExtractFrontmatter(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return "";
        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        return endIndex < 0 ? "" : content.Substring(3, endIndex - 3);
    }

    private static SkillViewMetadata ParseSkillViewMetadata(string frontmatter)
    {
        var tags = new List<string>();
        var related = new List<string>();
        var requiredEnv = new List<Dictionary<string, string>>();
        var requiredCredFiles = new List<string>();
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var inRequiredEnv = false;
        var inCredentialFiles = false;
        Dictionary<string, string>? currentEnv = null;

        foreach (var rawLine in frontmatter.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (!char.IsWhiteSpace(line[0]) && !trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                inRequiredEnv = trimmed.StartsWith("required_environment_variables:", StringComparison.OrdinalIgnoreCase);
                inCredentialFiles = trimmed.StartsWith("required_credential_files:", StringComparison.OrdinalIgnoreCase);
            }

            if (trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                tags = ParseTagList(trimmed.Split(':', 2)[1]);
            else if (trimmed.StartsWith("related_skills:", StringComparison.OrdinalIgnoreCase))
                related = ParseTagList(trimmed.Split(':', 2)[1]);
            else if (trimmed.StartsWith("compatibility:", StringComparison.OrdinalIgnoreCase))
                metadata["compatibility"] = trimmed.Split(':', 2)[1].Trim();

            if (inCredentialFiles)
            {
                if (trimmed.StartsWith("- path:", StringComparison.OrdinalIgnoreCase))
                    requiredCredFiles.Add(trimmed.Split(':', 2)[1].Trim().Trim('"', '\''));
                else if (trimmed.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
                    requiredCredFiles.Add(trimmed.Split(':', 2)[1].Trim().Trim('"', '\''));
            }

            if (inRequiredEnv)
            {
                if (trimmed.StartsWith("- name:", StringComparison.OrdinalIgnoreCase))
                {
                    currentEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["name"] = trimmed.Split(':', 2)[1].Trim().Trim('"', '\'')
                    };
                    requiredEnv.Add(currentEnv);
                }
                else if (currentEnv is not null && trimmed.Contains(':', StringComparison.Ordinal))
                {
                    var parts = trimmed.Split(':', 2);
                    currentEnv[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
                }
            }
        }

        if (tags.Count > 0 || related.Count > 0)
        {
            metadata["hermes"] = new Dictionary<string, object?>
            {
                ["tags"] = tags,
                ["related_skills"] = related
            };
        }

        return new SkillViewMetadata(tags, related, requiredEnv, requiredCredFiles, metadata);
    }

    private static List<string> ParseTagList(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            trimmed = trimmed[1..^1];

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim().Trim('"', '\''))
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static bool LooksBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
            return false;
        if (bytes.Contains((byte)0))
            return true;

        var sampleSize = Math.Min(bytes.Length, 1024);
        var control = 0;
        for (var i = 0; i < sampleSize; i++)
        {
            var b = bytes[i];
            if (b < 0x09 || (b > 0x0D && b < 0x20))
                control++;
        }

        return control > sampleSize / 10;
    }

    private static void ValidateSkillFrontmatterName(string content, string expectedName)
    {
        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        var yaml = content.Substring(3, endIndex - 3);
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                continue;

            var actual = trimmed.Split(':', 2)[1].Trim().Trim('"', '\'');
            if (!string.Equals(actual, expectedName, StringComparison.Ordinal))
                throw new ArgumentException($"Skill frontmatter name '{actual}' must match requested name '{expectedName}'.");
            return;
        }
    }

    private static void ValidateSupportingFileContent(string content)
    {
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(content);
        if (byteCount > 1_048_576)
            throw new ArgumentException("Supporting file content exceeds the 1 MiB limit.");
        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Supporting file content too long (max {MaxContentLength} chars)");
    }

    private string ResolveSkillFilePath(Skill skill, string filePath, bool requireSupportingSubdir)
    {
        if (!Path.GetFileName(skill.FilePath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Supporting files are only available for directory-based skills with SKILL.md.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("file_path is required.");
        if (Path.IsPathRooted(filePath) || HasTraversalComponent(filePath))
            throw new ArgumentException("Path traversal ('..') is not allowed.");

        var normalizedParts = filePath.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requireSupportingSubdir && normalizedParts.Length < 2)
            throw new ArgumentException("Provide a file path under references, templates, scripts, or assets.");
        if (requireSupportingSubdir && !AllowedSupportingSubdirs.Contains(normalizedParts[0]))
            throw new ArgumentException("File must be under one of: assets, references, scripts, templates.");

        var root = GetSkillRootDirectory(skill);
        var candidate = ResolvePathFollowingLinks(Path.Combine(new[] { root }.Concat(normalizedParts).ToArray()));
        var fullRoot = ResolvePathFollowingLinks(root);
        if (!IsPathWithinDirectory(candidate, fullRoot))
            throw new ArgumentException("Resolved file path escapes the skill directory.");

        return candidate;
    }

    private static string GetSkillRootDirectory(Skill skill)
        => Path.GetDirectoryName(skill.FilePath)!;

    private void TryDeleteEmptyCategoryDirectory(string deletedSkillRoot)
    {
        var parent = Path.GetDirectoryName(deletedSkillRoot);
        if (string.IsNullOrWhiteSpace(parent))
            return;

        var fullParent = ResolvePathFollowingLinks(parent);
        var fullSkills = ResolvePathFollowingLinks(_skillsDir);
        if (fullParent.Equals(fullSkills, StringComparison.OrdinalIgnoreCase))
            return;
        if (IsPathWithinDirectory(fullParent, fullSkills) &&
            Directory.Exists(fullParent) &&
            !Directory.EnumerateFileSystemEntries(fullParent).Any())
        {
            Directory.Delete(fullParent);
        }
    }

    private static async Task AtomicWriteTextAsync(string path, string content, CancellationToken ct)
    {
        var tempPath = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static bool IsPathWithinDirectory(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative == "." ||
               (!Path.IsPathRooted(relative) &&
                !relative.StartsWith("..", StringComparison.Ordinal) &&
                !relative.Equals("..", StringComparison.Ordinal));
    }

    private static string ResolvePathFollowingLinks(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root))
            return full;

        var current = root;
        var relative = Path.GetRelativePath(root, full);
        if (relative == ".")
            return full;

        foreach (var part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     .Where(part => !string.IsNullOrWhiteSpace(part)))
        {
            current = Path.Combine(current, part);
            if (File.Exists(current) || Directory.Exists(current))
                current = ResolveExistingPath(current);
        }

        return Path.GetFullPath(current);
    }

    private static string ResolveExistingPath(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
            return Path.GetFullPath(resolved?.FullName ?? info.FullName);
        }
        catch
        {
            return Path.GetFullPath(path);
        }
    }

    private static bool HasTraversalComponent(string path)
        => path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part == "..");
}

// =============================================
// Skill Types
// =============================================

public sealed class Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string FilePath { get; init; }
    public required List<string> Tools { get; init; }
    public string? Model { get; init; }
    public required string SystemPrompt { get; init; }
}

public sealed class SkillFrontmatter
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tools { get; set; } = "";
    public string Model { get; set; } = "";
}

public sealed record SkillPatchResult(
    Skill Skill,
    int MatchCount,
    string Strategy,
    string TargetLabel);

public sealed record SkillFileView(
    string File,
    string Content,
    string FileType,
    bool IsBinary);

public sealed record SkillViewMetadata(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RelatedSkills,
    IReadOnlyList<Dictionary<string, string>> RequiredEnvironmentVariables,
    IReadOnlyList<string> RequiredCredentialFiles,
    IReadOnlyDictionary<string, object?> Metadata);

// =============================================
// Exceptions
// =============================================

public sealed class SkillNotFoundException : Exception
{
    public SkillNotFoundException(string skillName) 
        : base($"Skill '{skillName}' not found")
    {
    }
}

public sealed class SkillPatchException : Exception
{
    public SkillPatchException(string message, string? filePreview = null)
        : base(message)
    {
        FilePreview = filePreview;
    }

    public string? FilePreview { get; }
}

// =============================================
// Skill Invoker
// =============================================

public sealed class SkillInvoker
{
    private readonly SkillManager _skillManager;
    private readonly IChatClient _chatClient;
    private readonly ILogger<SkillInvoker> _logger;
    
    public SkillInvoker(
        SkillManager skillManager,
        IChatClient chatClient,
        ILogger<SkillInvoker> logger)
    {
        _skillManager = skillManager;
        _chatClient = chatClient;
        _logger = logger;
    }
    
    /// <summary>
    /// Invoke skill and get response.
    /// </summary>
    public async Task<string> InvokeAsync(
        string skillName,
        string userQuery,
        CancellationToken ct)
    {
        var skillContext = await _skillManager.InvokeSkillAsync(skillName, userQuery, ct);
        
        var messages = new List<Message>
        {
            new Message { Role = "system", Content = skillContext }
        };
        
        var response = await _chatClient.CompleteAsync(messages, ct);
        
        _logger.LogInformation("Skill {Name} completed", skillName);
        
        return response;
    }
}

