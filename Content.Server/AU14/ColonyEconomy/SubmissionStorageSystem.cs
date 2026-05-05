using Content.Server.AU14.Ambassador;
using Content.Server.Containers;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Containers;
using Content.Shared.Stacks;
using Content.Shared.Tag;
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

        if (!TryComp<TagComponent>(args.Entity, out var tags))
            return;
        if (submission.Rewards is null)
            return;

        float sum = 0f;
        int num = 0;
        foreach (var tag in tags.Tags)
        {
            if (submission.Rewards.TryGetValue(tag, out var val))
            {
                sum += val;
                num++;
            }
        }
        // can never be too careful
        if (num == 0)
            num = 1;

        // e.g. $10 + $15 != $25 instead it equals $12.5
        float amount = sum / num;

        var mult = _ambassador.GetSubmissionMultiplier();
        var tariff = _corporateConsole.GetTariff();
        //var amount = submission.Rewards.TryGetValue()

        float reward;
        if (EntityManager.TryGetComponent<StackComponent>(args.Entity, out var stack))
            reward = amount * stack.Count * mult;
        else
            reward = amount * mult;

        EntityManager.PredictedQueueDeleteEntity(args.Entity);

        // Split: tariff % goes to corporate budget, remainder to colony budget
        var tariffAmount = reward * tariff;
        var colonyAmount = reward - tariffAmount;

        _colonyBudget.AddToBudget(colonyAmount);
        if (tariffAmount > 0f)
            _corporateConsole.AddToCorporateBudget(tariffAmount);
    }
}
