using Hermes.Agent.Runtime;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Services;

public sealed class NpcDeveloperInspectorResourceText : INpcDeveloperInspectorText
{
    private readonly ResourceLoader _resourceLoader = new();

    public string FileMissing => GetString(nameof(FileMissing));

    public string FileLoaded => GetString(nameof(FileLoaded));

    public string FileTruncatedFormat => GetString(nameof(FileTruncatedFormat));

    public string FileReadFailedFormat => GetString(nameof(FileReadFailedFormat));

    public string TraceLogMissing => GetString(nameof(TraceLogMissing));

    public string TraceParseFailedFormat => GetString(nameof(TraceParseFailedFormat));

    public string TraceEmptyForNpc => GetString(nameof(TraceEmptyForNpc));

    public string TraceSelectionMissing => GetString(nameof(TraceSelectionMissing));

    public string TranscriptSessionMissing => GetString(nameof(TranscriptSessionMissing));

    public string ModelReplyEmpty => GetString(nameof(ModelReplyEmpty));

    public string ToolCallEmpty => GetString(nameof(ToolCallEmpty));

    public string DelegationEmpty => GetString(nameof(DelegationEmpty));

    public string TodoEmpty => GetString(nameof(TodoEmpty));

    public string ReasoningMissing => GetString(nameof(ReasoningMissing));

    public string ToolResultMissing => GetString(nameof(ToolResultMissing));

    public string TraceKindObservation => GetString(nameof(TraceKindObservation));

    public string TraceKindModelRequest => GetString(nameof(TraceKindModelRequest));

    public string TraceKindModelReply => GetString(nameof(TraceKindModelReply));

    public string TraceKindIntent => GetString(nameof(TraceKindIntent));

    public string TraceKindLocalExecutor => GetString(nameof(TraceKindLocalExecutor));

    public string TraceKindToolCall => GetString(nameof(TraceKindToolCall));

    public string TraceKindBridge => GetString(nameof(TraceKindBridge));

    public string TraceKindResult => GetString(nameof(TraceKindResult));

    public string TraceKindDiagnostic => GetString(nameof(TraceKindDiagnostic));

    public string TraceKindRaw => GetString(nameof(TraceKindRaw));

    private string GetString(string propertyName)
        => _resourceLoader.GetString("DeveloperInspector" + propertyName);
}
