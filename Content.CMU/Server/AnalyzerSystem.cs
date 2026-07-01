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
public sealed partial class AnalyzerSystem : EntitySystem
{
    [Dependency] private AuFetchObjectiveSystem _fetchSystem = default!;
    [Dependency] private AuObjectiveSystem _objectiveSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;

    private const string ClfFaction = "clf";
    private const int CashPerPoint = 15;

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
        // Only CLF analyzers accept submissions.
        if (component.Faction.ToLowerInvariant() != ClfFaction)
            return;

        // A faction can configure exactly what may be submitted and at what ratio. When it has, the
        // classic dollars path is replaced by that table; otherwise fall back to plain dollars.
        if (component.Conversions.Count > 0)
        {
            OnConfiguredInserted(uid, component, args.Entity);
            return;
        }

        // Count inserted credits (stack or single bill).
        int credits = 1;
        if (TryComp(args.Entity, out StackComponent? stack))
            credits = stack.Count;

        // Bank partial credits; convert full CashPerPoint batches into win points.
        component.CashStored += credits;
        int points = component.CashStored / CashPerPoint;
        component.CashStored -= points * CashPerPoint;

        QueueDel(args.Entity);

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

    // Configured submission: only entities in the faction's conversion table convert, each at its own
    // ratio, with a separate banked remainder so different goods never spill into each other. Anything
    // not in the table is left alone in the slot for the player to take back out.
    private void OnConfiguredInserted(EntityUid uid, AnalyzerComponent component, EntityUid inserted)
    {
        var protoId = MetaData(inserted).EntityPrototype?.ID;
        if (protoId == null)
            return;

        AnalyzerConversionEntry? match = null;
        foreach (var entry in component.Conversions)
        {
            if (string.Equals(entry.Entity.Id, protoId, System.StringComparison.Ordinal))
            {
                match = entry;
                break;
            }
        }

        if (match == null)
            return;

        // Guard the ratio: a zero or negative amount-per-point would mint infinite points.
        var perPoint = System.Math.Max(1, match.AmountPerPoint);

        var amount = 1;
        if (TryComp(inserted, out StackComponent? stack))
            amount = stack.Count;

        var name = Name(inserted);

        var banked = component.Banked.GetValueOrDefault(protoId) + amount;
        var points = banked / perPoint;
        banked -= points * perPoint;
        component.Banked[protoId] = banked;

        QueueDel(inserted);

        if (points > 0)
            _objectiveSystem.AwardRawPointsToFaction(ClfFaction, points);

        var msg = points > 0
            ? $"Analyzer credited {points} point(s) to CLF. ({banked}/{perPoint} until next point)"
            : $"Analyzer banked {amount} {name}. ({banked}/{perPoint} until next point)";

        _popupSystem.PopupEntity(msg, uid);
    }
}
