using Content.Server.AU14.Ambassador;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class SubmissionStorageSystem : EntitySystem
{
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly AmbassadorConsoleSystem _ambassador = default!;
    [Dependency] private readonly CorporateConsoleSystem _corporateConsole = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubmissionStorageComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
    }

    private void OnEntityInserted(EntityUid uid,
        SubmissionStorageComponent storage,
        EntInsertedIntoContainerMessage args)
    {
        if (!EntityManager.TryGetComponent(uid, out SubmissionStorageComponent? submission))
            return;

        var mult = _ambassador.GetSubmissionMultiplier();
        var tariff = _corporateConsole.GetTariff();

        float reward;
        if (EntityManager.TryGetComponent<StackComponent>(args.Entity, out var stack))
            reward = submission.RewardAmount * stack.Count * mult;
        else
            reward = submission.RewardAmount * mult;

        EntityManager.QueueDeleteEntity(args.Entity);

        // Split: tariff % goes to corporate budget, remainder to colony budget
        var tariffAmount = reward * tariff;
        var colonyAmount = reward - tariffAmount;

        _colonyBudget.AddToBudget(colonyAmount);
        if (tariffAmount > 0f)
            _corporateConsole.AddToCorporateBudget(tariffAmount);
    }
}
