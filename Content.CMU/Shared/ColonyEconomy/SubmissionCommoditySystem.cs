using System.Diagnostics.CodeAnalysis;
using Content.Shared.Disposal.Unit;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

// works out which commodity an item sells as. shared so the client predicts the insert veto
public sealed partial class SubmissionCommoditySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private readonly List<CMUCommodityPrototype> _commodities = new();

    public IReadOnlyList<CMUCommodityPrototype> Commodities => _commodities;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubmissionStorageComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        // the disposal unit opens its own (missing) flush ui on click, get in first
        SubscribeLocalEvent<SubmissionStorageComponent, ActivateInWorldEvent>(OnActivate,
            before: [typeof(SharedDisposalUnitSystem)]);
        SubscribeLocalEvent<SubmissionStorageComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        CacheCommodities();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUCommodityPrototype>())
            CacheCommodities();
    }

    private void CacheCommodities()
    {
        _commodities.Clear();
        _commodities.AddRange(_proto.EnumeratePrototypes<CMUCommodityPrototype>());
        _commodities.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
    }

    public bool Buys(SubmissionStorageComponent point, CMUCommodityPrototype commodity)
    {
        if (point.Commodities.Contains(commodity.ID))
            return true;

        foreach (var category in commodity.Categories)
        {
            if (point.Categories.Contains(category))
                return true;
        }

        return false;
    }

    // regular commodities first, the fallback ones only when nothing specific matched.
    // the best matching tag wins, so a pumpkin (pumpkin + fruit + vegetable) sells as a pumpkin
    public bool TryMatch(SubmissionStorageComponent point, EntityUid item,
        [NotNullWhen(true)] out CMUCommodityPrototype? commodity, out float basePrice)
    {
        commodity = null;
        basePrice = 0f;
        if (!TryComp<TagComponent>(item, out var tags) || tags.Tags.Count == 0)
            return false;

        if (_whitelist.IsWhitelistPass(point.Blacklist, item))
            return false;

        for (var pass = 0; pass < 2; pass++)
        {
            var fallback = pass == 1;
            foreach (var candidate in _commodities)
            {
                if (candidate.Fallback != fallback || !Buys(point, candidate))
                    continue;

                foreach (var tag in tags.Tags)
                {
                    if (candidate.Prices.TryGetValue(tag, out var price) && price > basePrice)
                    {
                        commodity = candidate;
                        basePrice = price;
                    }
                }
            }

            if (commodity != null)
                return true;
        }

        return false;
    }

    // anything the point wouldn't pay for bounces, before it used to vanish for $0
    // or sit in the container forever
    private void OnInsertAttempt(Entity<SubmissionStorageComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryMatch(ent.Comp, args.EntityUid, out _, out _))
            args.Cancel();
    }

    private void OnActivate(Entity<SubmissionStorageComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !_ui.HasUi(ent, SubmissionMarketUiKey.Key))
            return;

        _ui.TryToggleUi(ent.Owner, SubmissionMarketUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnExamined(Entity<SubmissionStorageComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("cmu-submission-market-examine"));
    }
}
