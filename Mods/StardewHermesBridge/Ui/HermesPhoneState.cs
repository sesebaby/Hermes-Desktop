namespace StardewHermesBridge.Ui;

using StardewHermesBridge.Bridge;

public enum HermesPhoneUiOwner
{
    None,
    PhoneOverlay,
    VanillaDialogue
}

public enum HermesPhoneFocusOwner
{
    None,
    PhoneTextInput,
    VanillaDialogue
}

public enum HermesPhoneOpenState
{
    PhoneClosed,
    PhoneIndicatorOnly,
    PhoneThreadPassiveOpen,
    PhoneReplyFocusActive
}

public sealed class HermesPhoneState
{
    private readonly Dictionary<string, HermesPhoneThread> _threads = new(StringComparer.OrdinalIgnoreCase);

    public HermesPhoneUiOwner UiOwner { get; private set; }
    public HermesPhoneFocusOwner FocusOwner { get; private set; }
    public HermesPhoneOpenState OpenState { get; private set; } = HermesPhoneOpenState.PhoneClosed;
    public string? VisibleThreadId { get; private set; }
    public bool KeyboardSubscriberOwnedByPhone { get; set; }
    public IReadOnlyDictionary<string, HermesPhoneThread> Threads => _threads;

    public void OpenThread(string npcName, string conversationId)
    {
        var thread = GetOrCreateThread(npcName, conversationId);
        OpenThread(thread);
    }

    public bool OpenLatestThread()
    {
        var thread = _threads.Values
            .OrderByDescending(candidate => candidate.UnreadCount > 0)
            .ThenByDescending(candidate => candidate.Messages.LastOrDefault()?.TimestampUtc ?? DateTime.MinValue)
            .FirstOrDefault();
        if (thread is null)
            return false;

        OpenThread(thread);
        return true;
    }

    public void OpenPhoneHome()
    {
        UiOwner = HermesPhoneUiOwner.PhoneOverlay;
        FocusOwner = HermesPhoneFocusOwner.None;
        OpenState = HermesPhoneOpenState.PhoneThreadPassiveOpen;
    }

    private void OpenThread(HermesPhoneThread thread)
    {
        VisibleThreadId = thread.ThreadId;
        UiOwner = HermesPhoneUiOwner.PhoneOverlay;
        FocusOwner = HermesPhoneFocusOwner.None;
        OpenState = HermesPhoneOpenState.PhoneThreadPassiveOpen;
        thread.UnreadCount = 0;
    }

    public HermesPhoneMessage AddIncomingMessage(string npcName, string text, string? conversationId, bool openThread)
    {
        var thread = GetOrCreateThread(npcName, conversationId);
        var message = new HermesPhoneMessage(npcName, text, Incoming: true, DateTime.UtcNow, conversationId);
        thread.Messages.Add(message);

        if (openThread)
        {
            VisibleThreadId = thread.ThreadId;
            UiOwner = HermesPhoneUiOwner.PhoneOverlay;
            FocusOwner = HermesPhoneFocusOwner.None;
            OpenState = HermesPhoneOpenState.PhoneThreadPassiveOpen;
            thread.UnreadCount = 0;
        }
        else
        {
            thread.UnreadCount++;
            if (OpenState == HermesPhoneOpenState.PhoneClosed)
                OpenState = HermesPhoneOpenState.PhoneIndicatorOnly;
        }

        return message;
    }

    public void AddOutgoingMessage(string npcName, string text, string? conversationId)
    {
        var thread = GetOrCreateThread(npcName, conversationId);
        thread.Messages.Add(new HermesPhoneMessage(npcName, text, Incoming: false, DateTime.UtcNow, conversationId));
        VisibleThreadId = thread.ThreadId;
        UiOwner = HermesPhoneUiOwner.PhoneOverlay;
        FocusOwner = HermesPhoneFocusOwner.None;
        OpenState = HermesPhoneOpenState.PhoneThreadPassiveOpen;
    }

    public void FocusReplyInput()
    {
        if (UiOwner == HermesPhoneUiOwner.PhoneOverlay && VisibleThreadId is not null)
        {
            FocusOwner = HermesPhoneFocusOwner.PhoneTextInput;
            OpenState = HermesPhoneOpenState.PhoneReplyFocusActive;
        }
    }

    public void ReleaseReplyInput()
    {
        if (FocusOwner == HermesPhoneFocusOwner.PhoneTextInput)
            FocusOwner = HermesPhoneFocusOwner.None;

        if (UiOwner == HermesPhoneUiOwner.PhoneOverlay && VisibleThreadId is not null)
            OpenState = HermesPhoneOpenState.PhoneThreadPassiveOpen;
    }

    public void ClosePhone()
    {
        UiOwner = HermesPhoneUiOwner.None;
        FocusOwner = HermesPhoneFocusOwner.None;
        VisibleThreadId = null;
        OpenState = HasUnread ? HermesPhoneOpenState.PhoneIndicatorOnly : HermesPhoneOpenState.PhoneClosed;
    }

    public bool HasUnread => _threads.Values.Any(thread => thread.UnreadCount > 0);

    private HermesPhoneThread GetOrCreateThread(string npcName, string? conversationId)
    {
        var threadId = BuildThreadId(npcName, conversationId);
        if (_threads.TryGetValue(threadId, out var existing))
            return existing;

        var created = new HermesPhoneThread(threadId, npcName, conversationId);
        _threads[threadId] = created;
        return created;
    }

    private static string BuildThreadId(string npcName, string? conversationId)
        => string.IsNullOrWhiteSpace(conversationId)
            ? npcName
            : $"{npcName}:{conversationId}";
}

public sealed class HermesPhoneThread
{
    public HermesPhoneThread(string threadId, string npcName, string? conversationId)
    {
        ThreadId = threadId;
        NpcName = npcName;
        ConversationId = conversationId;
    }

    public string ThreadId { get; }
    public string NpcName { get; }
    public string? ConversationId { get; }
    public int UnreadCount { get; set; }
    public List<HermesPhoneMessage> Messages { get; } = new();
}

public sealed record HermesPhoneMessage(
    string NpcName,
    string Text,
    bool Incoming,
    DateTime TimestampUtc,
    string? ConversationId);
