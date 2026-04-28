namespace Hermes.Agent.Core;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Model-facing memory and session-search text copied from the Python reference.
/// Keep these strings centralized so the system prompt, tools, and background
/// review path do not drift independently.
/// </summary>
public static class MemoryReferenceText
{
    public const string DeclarativeMemoryGuidance =
        "Write memories as declarative facts, not instructions to yourself. " +
        "'User prefers concise responses' is correct; 'Always respond concisely' is not. " +
        "'Project uses pytest with xdist' is correct; 'Run tests with pytest -n 4' is not. " +
        "Imperative phrasing gets re-read as a directive in later sessions and can cause repeated work or override the user's current request. " +
        "Procedures and workflows belong in skills, not memory.";

    public const string MemoryGuidance =
        "You have persistent memory across sessions. Save durable facts using the memory tool: " +
        "user preferences, environment details, tool quirks, and stable conventions. " +
        "Memory is injected into every turn, so keep it compact and focused on facts that will still matter later.\n" +
        "Prioritize what reduces future user steering -- the most valuable memory is one that prevents the user from having to correct or remind you again. " +
        "User preferences and recurring corrections matter more than procedural task details.\n" +
        "Do NOT save task progress, session outcomes, completed-work logs, or temporary TODO state to memory; " +
        "use session_search to recall those from past transcripts. If you've discovered a new way to do something, solved a problem that could be necessary later, " +
        "save it as a skill with the skill tool.\n" +
        DeclarativeMemoryGuidance;

    public const string SessionSearchGuidance =
        "When the user references something from a past conversation or you suspect relevant cross-session context exists, " +
        "use session_search to recall it before asking them to repeat themselves.";

    public const string SkillsGuidance =
        "After completing a complex task (5+ tool calls), fixing a tricky error, " +
        "or discovering a non-trivial workflow, save the approach as a " +
        "skill with skill_manage so you can reuse it next time.\n" +
        "When using a skill and finding it outdated, incomplete, or wrong, " +
        "patch it immediately with skill_manage(action='patch') -- don't wait to be asked. " +
        "Skills that aren't maintained become liabilities.";

    public const string MemoryToolDescription =
        "Save durable information to persistent memory that survives across sessions. " +
        "Memory is injected into future turns, so keep it compact and focused on facts that will still matter later.\n\n" +
        "WHEN TO SAVE (do this proactively, don't wait to be asked):\n" +
        "- User corrects you or says 'remember this' / 'don't do that again'\n" +
        "- User shares a preference, habit, or personal detail (name, role, timezone, coding style)\n" +
        "- You discover something about the environment (OS, installed tools, project structure)\n" +
        "- You learn a convention, API quirk, or workflow specific to this user's setup\n" +
        "- You identify a stable fact that will be useful again in future sessions\n\n" +
        "PRIORITY: User preferences and corrections > environment facts > procedural knowledge. " +
        "The most valuable memory prevents the user from having to repeat themselves.\n\n" +
        "Do NOT save task progress, session outcomes, completed-work logs, or temporary TODO state to memory; " +
        "use session_search to recall those from past transcripts.\n" +
        "If you've discovered a new way to do something, solved a problem that could be necessary later, " +
        "save it as a skill with the skill tool.\n\n" +
        "TWO TARGETS:\n" +
        "- 'user': who the user is -- name, role, preferences, communication style, pet peeves\n" +
        "- 'memory': your notes -- environment facts, project conventions, tool quirks, lessons learned\n\n" +
        "ACTIONS: add (new entry), replace (update existing -- old_text identifies it), remove (delete -- old_text identifies it).\n\n" +
        "SKIP: trivial/obvious info, things easily re-discovered, raw data dumps, and temporary task state.\n\n" +
        DeclarativeMemoryGuidance;

    public const string SessionSearchToolDescription =
        "Search your long-term memory of past conversations, or browse recent sessions. This is your recall -- " +
        "every past session is searchable, and this tool summarizes what happened.\n\n" +
        "TWO MODES:\n" +
        "1. Recent sessions (no query): Call with no arguments to see what was worked on recently. " +
        "Returns titles, previews, and timestamps. Zero LLM cost, instant. " +
        "Start here when the user asks what were we working on or what did we do recently.\n" +
        "2. Keyword search (with query): Search for specific topics across all past sessions. " +
        "Returns LLM-generated summaries of matching sessions.\n\n" +
        "USE THIS PROACTIVELY when:\n" +
        "- The user says 'we did this before', 'remember when', 'last time', 'as I mentioned'\n" +
        "- The user asks about a topic you worked on before but don't have in current context\n" +
        "- The user references a project, person, or concept that seems familiar but isn't in memory\n" +
        "- You want to check if you've solved a similar problem before\n" +
        "- The user asks 'what did we do about X?' or 'how did we fix Y?'\n\n" +
        "Don't hesitate to search when it is actually cross-session -- it's fast and cheap. " +
        "Better to search and confirm than to guess or ask the user to repeat themselves.\n\n" +
        "Search syntax: keywords joined with OR for broad recall (elevenlabs OR baseten OR funding), " +
        "phrases for exact match (\"docker networking\"), boolean (python NOT java), prefix (deploy*). " +
        "IMPORTANT: Use OR between keywords for best results -- FTS5 defaults to AND which misses " +
        "sessions that only mention some terms. If a broad OR query returns nothing, try individual " +
        "keyword searches in parallel. Returns summaries of the top matching sessions.";

    public static JsonElement BuildMemoryToolParameterSchema()
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "add", "replace", "remove" },
                    ["description"] = "The action to perform."
                },
                ["target"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "memory", "user" },
                    ["description"] = "Which memory store: 'memory' for personal notes, 'user' for user profile."
                },
                ["content"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The entry content. Required for 'add' and 'replace'."
                },
                ["old_text"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Short unique substring identifying the entry to replace or remove."
                }
            },
            ["required"] = new[] { "action", "target" }
        };

        return JsonSerializer.SerializeToElement(schema, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
