using Content.Server.Popups;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Intel;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Intel;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Round.Objectives.Type;

/// <summary>
/// Handles the Analyzer Machine:
///     All factions get a "Scan" context action that credits nearby active fetch objs
///     Using an RMC intel item on it credits that intel to the Analyzer's faction
///     CLF only: cash can be inserted & every 15 creds awards 1 win point directly to CLF victory
/// </summary>
public sealed partial class ObjFetchAnalyzerSystem : EntitySystem
{
    [Dependency] private ObjFetchSystem _fetchSystem = default!;
    [Dependency] private ObjectiveControlSystem _objCtrl = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private SharedCMChatSystem _chat = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string ClfFaction = "clf";
    private const int CashPerPoint = 15;

    private static readonly ProtoId<TagPrototype> CurrencyTag = "Currency";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FetchAnalyzerComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FetchAnalyzerComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<FetchAnalyzerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FetchAnalyzerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FetchAnalyzerComponent, PowerChangedEvent>(OnPowerChanged);
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
        var fetched = new List<EntityUid>();
        var count = _fetchSystem.ScanForFetchItems(analyzerUid, fetched);
        foreach (var item in fetched)
        {
            if (TryComp(item, out CMUIntelSurveyOnAnalyzeComponent? survey))
            {
                PrintIntelSurvey(analyzerUid, (item, survey));
                _popupSystem.PopupEntity("The Analyzer prints out an intel survey.", analyzerUid, user);
            }
        }

        var message = count > 0
            ? $"Analyzer detected {count} item(s) of interest in the vicinity."
            : "No items of interest detected nearby.";
        _popupSystem.PopupEntity(message, analyzerUid, user);

        if (count > 0)
            StartWorking(analyzerUid);
    }

    /// <summary>
    /// Using an RMC intel document or device on the Analyzer credits it to the Analyzer's faction, and tells
    /// the user where the intel it unlocked can be found.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, FetchAnalyzerComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var itemName = Name(args.Used);
        switch (_fetchSystem.TryFetchAtAnalyzer(uid, args.Used))
        {
            case FetchAnalyzeResult.Fetched:
                args.Handled = true;
                _popupSystem.PopupEntity($"Analyzer scanned the {itemName}. Objective item recovered.", uid, args.User);
                StartWorking(uid);
                if (TryComp(args.Used, out CMUIntelSurveyOnAnalyzeComponent? survey))
                {
                    PrintIntelSurvey(uid, (args.Used, survey));
                    _popupSystem.PopupEntity("The Analyzer prints out an intel survey.", uid, args.User);
                }
                return;
            case FetchAnalyzeResult.AlreadyFetched:
                args.Handled = true;
                _popupSystem.PopupEntity($"The {itemName} has already been scanned.", uid, args.User);
                return;
            case FetchAnalyzeResult.WrongFaction:
                args.Handled = true;
                _popupSystem.PopupEntity($"This Analyzer has no use for the {itemName}.", uid, args.User);
                return;
        }

        if (HasComp<CMUIntelDataDiskComponent>(args.Used))
        {
            args.Handled = true;
            _popupSystem.PopupEntity($"The Analyzer can't read the {itemName}. It's encrypted; use an intel computer.", uid, args.User);
            return;
        }

        if (string.IsNullOrEmpty(component.Faction))
            return;

        var clues = new List<string>();
        var result = _intel.AnalyzeIntel(args.Used, component.Faction.ToLowerInvariant(),
            component.IntelPointMultiplier, clues, out var points);
        if (result == IntelAnalyzeResult.NotIntel)
            return;

        args.Handled = true;
        var name = Name(args.Used);
        switch (result)
        {
            case IntelAnalyzeResult.Locked:
                _popupSystem.PopupEntity($"The Analyzer can't make sense of the {name} yet. Find and process the intel that points to it first.", uid, args.User);
                return;
            case IntelAnalyzeResult.AlreadyDone:
                _popupSystem.PopupEntity($"The {name} has already been analyzed.", uid, args.User);
                return;
        }

        _popupSystem.PopupEntity($"Analyzer processed the {name} for {points.Double():0.##} intel point(s).", uid, args.User);
        foreach (var clue in clues)
            _chat.ChatMessageToOne(clue, args.User);

        StartWorking(uid);
    }

    private void StartWorking(EntityUid uid)
    {
        if (!TryComp(uid, out FetchAnalyzerComponent? component))
            return;

        component.WorkingUntil = _timing.CurTime + component.WorkingDuration;
        UpdateVisuals(uid, component);
    }

    private void OnMapInit(EntityUid uid, FetchAnalyzerComponent component, MapInitEvent args)
        => UpdateVisuals(uid, component);

    private void OnPowerChanged(EntityUid uid, FetchAnalyzerComponent component, ref PowerChangedEvent args)
        => UpdateVisuals(uid, component);

    private void UpdateVisuals(EntityUid uid, FetchAnalyzerComponent component)
    {
        FetchAnalyzerVisualState state;
        if (!_power.IsPowered(uid))
            state = FetchAnalyzerVisualState.Off;
        else if (component.WorkingUntil is { } until && _timing.CurTime < until)
            state = FetchAnalyzerVisualState.Working;
        else
            state = FetchAnalyzerVisualState.Idle;

        _appearance.SetData(uid, FetchAnalyzerVisuals.State, state);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FetchAnalyzerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.WorkingUntil is not { } until || now < until)
                continue;

            component.WorkingUntil = null;
            UpdateVisuals(uid, component);
        }
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
