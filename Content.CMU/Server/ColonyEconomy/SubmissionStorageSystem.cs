using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.CorporateRelations;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class SubmissionStorageSystem : EntitySystem
{
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private AmbassadorConsoleSystem _ambassador = default!;
    [Dependency] private CorporateConsoleSystem _corporateConsole = default!;
    [Dependency] private CMUCorporateRelationsSystem _relations = default!;
    [Dependency] private SubmissionCommoditySystem _commodity = default!;
    [Dependency] private SubmissionMarketSystem _market = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubmissionStorageComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
    }

    private void OnEntityInserted(EntityUid uid,
        SubmissionStorageComponent storage,
        EntInsertedIntoContainerMessage args)
    {
        // the insert veto already bounced anything that doesn't match
        if (!_commodity.TryMatch(storage, args.Entity, out var commodity, out var basePrice))
            return;

        var count = TryComp<StackComponent>(args.Entity, out var stack) ? stack.Count : 1;
        var baseValue = basePrice * count;

        // priced before the sale counts, so it pays what the board showed
        // CMU14: a withdrawn WY leaves the colony with worse prices.
        var reward = baseValue * _market.GetMultiplier(commodity) * _ambassador.GetSubmissionMultiplier() * _relations.ColonyPayoutMultiplier;
        _market.RecordSale(commodity, baseValue);

        _relations.OnDelivery(args.Entity, count, reward);
        PredictedQueueDel(args.Entity);

        // Split: tariff % goes to corporate budget, remainder to colony budget
        // A withdrawn WY no longer takes its cut.
        var tariff = _relations.Withdrawn ? 0f : _corporateConsole.GetTariff();
        var tariffAmount = reward * tariff;
        var colonyAmount = reward - tariffAmount;

        _colonyBudget.AddToBudget(colonyAmount);
        if (tariffAmount > 0f)
            _corporateConsole.AddToCorporateBudget(tariffAmount);
    }
}
