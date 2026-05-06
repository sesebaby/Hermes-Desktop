namespace Hermes.Agent.Core;

public interface IFirstCallContextBudgetPolicy
{
    FirstCallContextBudgetResult Apply(FirstCallContextBudgetRequest request);
}

public interface IOutboundContextCompactionPolicy
{
    ContextCompactionResult Apply(ContextCompactionRequest request);
}

public sealed record FirstCallContextBudgetRequest(
    Session Session,
    IReadOnlyList<Message> Messages,
    string CurrentUserMessage,
    int Iteration);

public sealed record ContextCompactionRequest(
    Session Session,
    IReadOnlyList<Message> Messages,
    string CurrentUserMessage,
    int Iteration);

public sealed record FirstCallContextBudgetResult(
    IReadOnlyList<Message> Messages,
    bool Applied,
    bool BudgetMet,
    string BudgetUnmetReason = "unknown");

public sealed record ContextCompactionResult(
    IReadOnlyList<Message> Messages,
    bool Applied,
    bool BudgetMet,
    string BudgetUnmetReason = "unknown");

public sealed class NoopFirstCallContextBudgetPolicy : IFirstCallContextBudgetPolicy
{
    public static NoopFirstCallContextBudgetPolicy Instance { get; } = new();

    private NoopFirstCallContextBudgetPolicy()
    {
    }

    public FirstCallContextBudgetResult Apply(FirstCallContextBudgetRequest request)
        => new(request.Messages, Applied: false, BudgetMet: true);
}

public sealed class NoopOutboundContextCompactionPolicy : IOutboundContextCompactionPolicy
{
    public static NoopOutboundContextCompactionPolicy Instance { get; } = new();

    private NoopOutboundContextCompactionPolicy()
    {
    }

    public ContextCompactionResult Apply(ContextCompactionRequest request)
        => new(request.Messages, Applied: false, BudgetMet: true);
}
