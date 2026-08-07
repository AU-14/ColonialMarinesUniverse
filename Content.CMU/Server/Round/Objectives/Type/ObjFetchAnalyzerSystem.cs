using Content.Server.Popups;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Round.Objectives.Type;

/// <summary>
/// Handles the Analyzer Machine:
///     All factions get a "Scan" context action that credits nearby active fetch objs
///     CLF only: cash can be inserted & every 15 creds awards 1 win point directly to CLF victory
/// </summary>
public sealed partial class ObjFetchAnalyzerSystem : EntitySystem
{
    [Dependency] private ObjFetchSystem _fetchSystem = default!;
    [Dependency] private ObjectiveControlSystem _objCtrl = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private TagSystem _tag = default!;

    private const string ClfFaction = "clf";
    private const int CashPerPoint = 15;

    private static readonly ProtoId<TagPrototype> CurrencyTag = "Currency";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FetchAnalyzerComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FetchAnalyzerComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
    }

    private void OnGetVerbs(EntityUid uid, FetchAnalyzerComponent component, GetVerbsEvent<InteractionVerb> args)
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

    private void OnCashInserted(EntityUid uid, FetchAnalyzerComponent component, EntInsertedIntoContainerMessage args)
    {
        if (component.Faction.ToLowerInvariant() != ClfFaction)
            return;

        if (component.Conversions.Count > 0 && TryCreditConfigured(uid, component, args.Entity))
            return;

        if (component.IncludeDollars && _tag.HasTag(args.Entity, CurrencyTag))
            CreditDollars(uid, component, args.Entity);
    }

    private bool TryCreditConfigured(EntityUid uid, FetchAnalyzerComponent component, EntityUid inserted)
    {
        var protoId = MetaData(inserted).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        AnalyzerConversionEntry? match = null;
        foreach (var entry in component.Conversions)
        {
            if (string.Equals(entry.Entity.Id, protoId, StringComparison.Ordinal))
            {
                match = entry;
                break;
            }
        }

        if (match == null)
            return false;

        var amount = 1;
        if (TryComp(inserted, out StackComponent? stack))
            amount = stack.Count;

        var name = Name(inserted);
        string msg;

        if (match.PointsPerItemMode)
        {
            var per = Math.Max(1, match.PointsPerItem);
            var points = amount * per;

            QueueDel(inserted);
            _objCtrl.AwardRawPointsToFaction(ClfFaction, points);
            msg = $"Analyzer credited {points} point(s) to CLF for {amount} {name}.";
        }
        else
        {
            var perPoint = Math.Max(1, match.AmountPerPoint);

            var banked = component.Banked.GetValueOrDefault(protoId) + amount;
            var points = banked / perPoint;
            banked -= points * perPoint;
            component.Banked[protoId] = banked;

            QueueDel(inserted);

            if (points > 0)
                _objCtrl.AwardRawPointsToFaction(ClfFaction, points);

            msg = points > 0
                ? $"Analyzer credited {points} point(s) to CLF. ({banked}/{perPoint} until next point)"
                : $"Analyzer banked {amount} {name}. ({banked}/{perPoint} until next point)";
        }

        _popupSystem.PopupEntity(msg, uid);
        return true;
    }

    private void CreditDollars(EntityUid uid, FetchAnalyzerComponent component, EntityUid inserted)
    {
        int credits = 1;
        if (TryComp(inserted, out StackComponent? stack))
            credits = stack.Count;

        component.CashStored += credits;
        int points = component.CashStored / CashPerPoint;
        component.CashStored -= points * CashPerPoint;

        QueueDel(inserted);

        if (points > 0)
            _objCtrl.AwardRawPointsToFaction(ClfFaction, points);

        var banked = component.CashStored > 0
            ? $" ({component.CashStored}/{CashPerPoint} cr. banked)"
            : string.Empty;

        var msg = points > 0
            ? $"Analyzer credited {points} point(s) to CLF.{banked}"
            : $"Analyzer banked {credits} cr. ({component.CashStored}/{CashPerPoint} cr. until next point).";

        _popupSystem.PopupEntity(msg, uid);
    }
}
