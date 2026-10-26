using Content.Server.CMU14.Ambassador;
using Content.Server.Containers;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Containers;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class SubmissionStorageSystem : EntitySystem
{
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private AmbassadorConsoleSystem _ambassador = default!;
    [Dependency] private CorporateConsoleSystem _corporateConsole = default!;
 
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubmissionStorageComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
    }



    private void OnEntityInserted(EntityUid uid,
        SubmissionStorageComponent storage,
        EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(uid, out SubmissionStorageComponent? submission))
            return;

        if (!TryComp<TagComponent>(args.Entity, out var tags))
            return;

        // best specific tag wins. this used to average every match, so a tomato
        // (tomato + fruit + vegetable) got dragged down to the generic price
        var amount = BestReward(submission.Rewards, tags.Tags);
        if (amount <= 0f)
            amount = BestReward(submission.FallbackRewards, tags.Tags);

        var mult = _ambassador.GetSubmissionMultiplier();
        var tariff = _corporateConsole.GetTariff();

        float reward;
        if (TryComp<StackComponent>(args.Entity, out var stack))
            reward = amount * stack.Count * mult;
        else
            reward = amount * mult;

        PredictedQueueDel(args.Entity);

        // Split: tariff % goes to corporate budget, remainder to colony budget
        var tariffAmount = reward * tariff;
        var colonyAmount = reward - tariffAmount;

        _colonyBudget.AddToBudget(colonyAmount);
        if (tariffAmount > 0f)
            _corporateConsole.AddToCorporateBudget(tariffAmount);
    }

    private static float BestReward(Dictionary<ProtoId<TagPrototype>, float>? rewards, IEnumerable<ProtoId<TagPrototype>> tags)
    {
        if (rewards is null)
            return 0f;

        var best = 0f;
        foreach (var tag in tags)
        {
            if (rewards.TryGetValue(tag, out var value) && value > best)
                best = value;
        }

        return best;
    }
}
