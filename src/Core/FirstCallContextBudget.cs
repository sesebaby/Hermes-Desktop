namespace Hermes.Agent.Core;

public interface IFirstCallContextBudgetPolicy
{
    FirstCallContextBudgetResult Apply(FirstCallContextBudgetRequest request);
}

public sealed record FirstCallContextBudgetRequest(
    Session Session,
    IReadOnlyList<Message> Messages,
    string CurrentUserMessage,
    int Iteration);

public sealed record FirstCallContextBudgetResult(
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
