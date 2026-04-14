using Content.Server.AU14.Objectives;
using Content.Server.AU14.Objectives.Fetch;
using Content.Server.Popups;
using Content.Shared.AU14;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Server.AU14;

/// <summary>
/// Handles the Analyzer Machine.
/// - All factions: "Scan" context-menu action that credits nearby items to active fetch objectives.
/// - CLF only: cash can be inserted; every 100 credits awards 1 win point directly to CLF.
/// </summary>
public sealed class AnalyzerSystem : EntitySystem
{
    [Dependency] private readonly AuFetchObjectiveSystem _fetchSystem = default!;
    [Dependency] private readonly AuObjectiveSystem _objectiveSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    private const string ClfFaction = "clf";
    private const int CashPerPoint = 30;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnalyzerComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AnalyzerComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
    }

    private void OnGetVerbs(EntityUid uid, AnalyzerComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var verb = new InteractionVerb
        {
            Act = () => PerformScan(uid, args.User),
            Text = "Scan",
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
        };
        args.Verbs.Add(verb);
    }

    private void PerformScan(EntityUid analyzerUid, EntityUid user)
    {
        var count = _fetchSystem.ScanForFetchItems(analyzerUid);
        var message = count > 0
            ? $"Analyzer detected {count} item(s) of interest in the vicinity."
            : "No items of interest detected nearby.";
        _popupSystem.PopupEntity(message, analyzerUid, user);
    }


    private void OnCashInserted(EntityUid uid, AnalyzerComponent component, EntInsertedIntoContainerMessage args)
    {
        // Only CLF analyzers accept cash.
        if (component.Faction.ToLowerInvariant() != ClfFaction)
            return;

        // Count inserted credits (stack or single bill).
        int credits = 1;
        if (TryComp(args.Entity, out StackComponent? stack))
            credits = stack.Count;

        // Bank partial credits; convert full CashPerPoint batches into win points.
        component.CashStored += credits;
        int points = component.CashStored / CashPerPoint;
        component.CashStored -= points * CashPerPoint;

        EntityManager.QueueDeleteEntity(args.Entity);

        if (points > 0)
            _objectiveSystem.AwardRawPointsToFaction(ClfFaction, points);

        var banked = component.CashStored > 0
            ? $" ({component.CashStored}/{CashPerPoint} cr. banked)"
            : string.Empty;

        var msg = points > 0
            ? $"Analyzer credited {points} point(s) to CLF.{banked}"
            : $"Analyzer banked {credits} cr. ({component.CashStored}/{CashPerPoint} cr. until next point).";

        _popupSystem.PopupEntity(msg, uid);
    }
}
