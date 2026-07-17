using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaMarkSystem : EntitySystem
{
    private const int MaxReasonLength = 120;
    private static readonly ProtoId<NpcFactionPrototype> YautjaBadBloodFaction = "CMUYautjaBadBlood";

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaOpenMarkPanelActionEvent>(OnOpenMarkPanel);
        SubscribeLocalEvent<YautjaBracerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<YautjaComponent, ComponentRemove>(OnYautjaRemoved);
        SubscribeLocalEvent<YautjaMarkComponent, EntityTerminatingEvent>(OnMarkedEntityTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        Subs.BuiEvents<YautjaBracerComponent>(YautjaMarkUIKey.Key, subs =>
        {
            subs.Event<YautjaMarkPanelRefreshMsg>(OnRefreshMsg);
            subs.Event<YautjaMarkPanelMarkMsg>(OnMarkMsg);
            subs.Event<YautjaMarkPanelUnmarkMsg>(OnUnmarkMsg);
        });
    }

    private void OnYautjaRemoved(Entity<YautjaComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;

        ClearHunterMarks(ent.Owner);
    }

    private void OnMarkedEntityTerminating(Entity<YautjaMarkComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;

        ClearTargetMarks(ent, deleteComponent: false, targetDestroyed: true);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var uid, out var mark))
            ClearTargetMarks((uid, mark));
    }

    private void OnOpenMarkPanel(Entity<YautjaBracerComponent> ent, ref YautjaOpenMarkPanelActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        if (!TryOpenMarkPanel(ent, args.Performer))
            return;

        args.Handled = true;
    }

    public bool TryOpenMarkPanel(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUsePanel(bracer, user))
            return false;

        _ui.TryOpenUi(bracer.Owner, YautjaMarkUIKey.Key, user);
        UpdateUi(bracer, user);
        return true;
    }

    private void OnUiOpened(Entity<YautjaBracerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, YautjaMarkUIKey.Key))
            return;

        UpdateUi(ent, args.Actor);
    }

    private void OnRefreshMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelRefreshMsg args)
    {
        UpdateUi(ent, args.Actor);
    }

    private void OnMarkMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelMarkMsg args)
    {
        if (_net.IsClient || !TryGetEntity(args.Target, out var target))
            return;

        if (!TryMark(ent, args.Actor, target.Value, args.Kind, args.Reason))
            return;

        UpdateUi(ent, args.Actor);
    }

    private void OnUnmarkMsg(Entity<YautjaBracerComponent> ent, ref YautjaMarkPanelUnmarkMsg args)
    {
        if (_net.IsClient || !TryGetEntity(args.Target, out var target))
            return;

        if (IsBadBloodHonorRestricted(args.Actor, args.Kind, popup: true))
            return;

        if (!CanUsePanel(ent, args.Actor) || !CanMarkTarget(args.Actor, target.Value, ent.Comp, args.Kind, false))
            return;

        if (!TryRemoveMark(target.Value, args.Kind, args.Actor, out _))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):actor} removed Yautja mark {args.Kind} from {ToPrettyString(target.Value):target}");

        UpdateUi(ent, args.Actor);
    }

    public bool TryMark(Entity<YautjaBracerComponent> bracer, EntityUid hunter, EntityUid target, YautjaMarkKind kind, string? reason)
    {
        if (IsBadBloodHonorRestricted(hunter, kind, popup: true))
            return false;

        if (!CanUsePanel(bracer, hunter) || !CanMarkTarget(hunter, target, bracer.Comp, kind, true))
            return false;

        if (kind == YautjaMarkKind.Prey && HunterHasPrey(hunter, target))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-mark-already-hunting"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var mark = EnsureComp<YautjaMarkComponent>(target);
        if (IsSingleOwnerMark(kind) &&
            mark.Marks.TryGetValue(kind, out var existingHunter) &&
            existingHunter != hunter)
        {
            mark.Reasons.TryGetValue(kind, out var existingReason);
            _popup.PopupEntity(
                Loc.GetString(GetAlreadyMarkedText(kind), ("target", target), ("hunter", existingHunter), ("reason", existingReason ?? string.Empty)),
                hunter,
                hunter,
                PopupType.SmallCaution);
            return false;
        }

        var trimmed = reason?.Trim();
        if (trimmed is { Length: > MaxReasonLength })
            trimmed = trimmed[..MaxReasonLength];

        if (RequiresReason(kind) && string.IsNullOrWhiteSpace(trimmed))
        {
            if (mark.Marks.Count == 0)
                RemCompDeferred<YautjaMarkComponent>(target);

            return false;
        }

        var attempt = new YautjaMarkAttemptEvent(hunter, target, kind, trimmed);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
        {
            if (mark.Marks.Count == 0)
                RemCompDeferred<YautjaMarkComponent>(target);

            return false;
        }

        mark.Marks[kind] = hunter;
        if (string.IsNullOrWhiteSpace(trimmed))
            mark.Reasons.Remove(kind);
        else
            mark.Reasons[kind] = trimmed;
        Dirty(target, mark);
        EnsureComp<StatusIconComponent>(target);

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(hunter):actor} applied Yautja mark {kind} to {ToPrettyString(target):target}");
        }
        else
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(hunter):actor} applied Yautja mark {kind} to {ToPrettyString(target):target} reason=\"{trimmed}\"");
        }

        _popup.PopupClient(Loc.GetString("cmu-yautja-mark-applied", ("target", target), ("kind", Loc.GetString(GetMarkName(kind)))), hunter, hunter);
        switch (kind)
        {
            case YautjaMarkKind.Prey:
                BroadcastPreyMark(hunter, target);
                break;
            case YautjaMarkKind.Honored:
            case YautjaMarkKind.Dishonored:
                BroadcastHonorTransition(hunter, target, kind, false, trimmed);
                break;
            case YautjaMarkKind.GearCarrier:
                BroadcastGearCarrierTransition(hunter, target, false);
                break;
        }

        var applied = new YautjaMarkAppliedEvent(hunter, target, kind, trimmed);
        RaiseLocalEvent(target, ref applied);

        return true;
    }

    private void BroadcastPreyMark(EntityUid hunter, EntityUid target)
    {
        var message = Loc.GetString(
            "cmu-yautja-mark-prey-broadcast",
            ("hunter", Name(hunter)),
            ("target", Name(target)),
            ("honor", YautjaHonorWorth.Get(target, EntityManager)),
            ("area", _areas.GetAreaName(target)));

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            if (!Deleted(yautja))
                _popup.PopupEntity(message, yautja, yautja, PopupType.Medium);
        }
    }

    public void ForceMark(EntityUid hunter, EntityUid target, YautjaMarkKind kind, bool addStatusIcon = true, string? reason = null)
    {
        if (_net.IsClient)
            return;

        var mark = EnsureComp<YautjaMarkComponent>(target);
        mark.Marks[kind] = hunter;
        var trimmed = reason?.Trim();
        if (trimmed is { Length: > MaxReasonLength })
            trimmed = trimmed[..MaxReasonLength];

        if (string.IsNullOrWhiteSpace(trimmed))
            mark.Reasons.Remove(kind);
        else
            mark.Reasons[kind] = trimmed;

        Dirty(target, mark);

        if (addStatusIcon)
            EnsureComp<StatusIconComponent>(target);
    }

    private static bool IsSingleOwnerMark(YautjaMarkKind kind)
    {
        return kind is YautjaMarkKind.Prey
            or YautjaMarkKind.Honored
            or YautjaMarkKind.Dishonored
            or YautjaMarkKind.GearCarrier;
    }

    private static bool RequiresReason(YautjaMarkKind kind)
    {
        return kind is YautjaMarkKind.Thrall or YautjaMarkKind.Blooded;
    }

    private static string GetAlreadyMarkedText(YautjaMarkKind kind)
    {
        return kind switch
        {
            YautjaMarkKind.Prey => "cmu-yautja-mark-prey-claimed",
            YautjaMarkKind.Honored => "cmu-yautja-mark-already-honored",
            YautjaMarkKind.Dishonored => "cmu-yautja-mark-already-dishonored",
            YautjaMarkKind.GearCarrier => "cmu-yautja-mark-already-gear-carrier",
            _ => "cmu-yautja-mark-already-marked",
        };
    }

    private bool CanUsePanel(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (bracer.Comp.User != user || !_inventory.InSlotWithFlags((bracer, null, null), bracer.Comp.Slots))
            return false;

        return true;
    }

    private bool CanMarkTarget(EntityUid hunter, EntityUid target, YautjaBracerComponent bracer, YautjaMarkKind kind, bool popup)
    {
        if (hunter == target)
            return false;

        if (_mob.IsDead(target))
            return false;

        var humanoid = HasComp<HumanoidAppearanceComponent>(target);
        var xeno = HasComp<XenoComponent>(target);
        if (!CanMarkSpecies(kind, target, humanoid, xeno))
            return false;

        var hunterCoords = _transform.GetMapCoordinates(hunter);
        var targetCoords = _transform.GetMapCoordinates(target);
        if (hunterCoords.MapId != targetCoords.MapId || (hunterCoords.Position - targetCoords.Position).LengthSquared() > 49)
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cmu-yautja-mark-too-far"), hunter, hunter, PopupType.SmallCaution);

            return false;
        }

        return true;
    }

    private bool CanMarkSpecies(YautjaMarkKind kind, EntityUid target, bool humanoid, bool xeno)
    {
        var strictHuman = humanoid && !HasComp<YautjaComponent>(target);

        return kind switch
        {
            YautjaMarkKind.Thrall => humanoid && !HasComp<YautjaComponent>(target),
            YautjaMarkKind.Blooded => humanoid && (!HasComp<YautjaComponent>(target) || HasComp<YautjaYoungbloodComponent>(target)),
            YautjaMarkKind.Prey => strictHuman || xeno,
            YautjaMarkKind.Honored => strictHuman,
            YautjaMarkKind.Dishonored => strictHuman || xeno,
            YautjaMarkKind.GearCarrier => humanoid,
            YautjaMarkKind.Student => HasComp<YautjaYoungbloodComponent>(target),
            _ => humanoid || xeno,
        };
    }

    private bool IsBadBloodHonorRestricted(EntityUid hunter, YautjaMarkKind kind, bool popup)
    {
        if (kind is not (YautjaMarkKind.Honored or YautjaMarkKind.Dishonored or YautjaMarkKind.Thrall or YautjaMarkKind.Blooded))
            return false;

        if (!TryComp(hunter, out NpcFactionMemberComponent? faction) ||
            !faction.Factions.Contains(YautjaBadBloodFaction))
        {
            return false;
        }

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-badblood-no-honor"), hunter, hunter, PopupType.SmallCaution);

        return true;
    }

    private bool HunterHasPrey(EntityUid hunter, EntityUid allowedTarget)
    {
        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (uid == allowedTarget)
                continue;

            if (mark.Marks.TryGetValue(YautjaMarkKind.Prey, out var otherHunter) && otherHunter == hunter)
                return true;
        }

        return false;
    }

    private void ClearHunterMarks(EntityUid hunter)
    {
        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            var toRemove = new List<YautjaMarkKind>();
            foreach (var (kind, markedHunter) in mark.Marks)
            {
                if (markedHunter == hunter)
                    toRemove.Add(kind);
            }

            foreach (var kind in toRemove)
                TryRemoveMark((uid, mark), kind, hunter, out _, raiseAttempt: false, cleanupOnly: true);
        }
    }

    private void ClearTargetMarks(Entity<YautjaMarkComponent> target, bool deleteComponent = true, bool targetDestroyed = false)
    {
        var marks = new List<(YautjaMarkKind Kind, EntityUid Hunter)>();
        foreach (var (kind, hunter) in target.Comp.Marks)
            marks.Add((kind, hunter));

        foreach (var (kind, hunter) in marks)
            TryRemoveMark(target, kind, hunter, out _, raiseAttempt: false, deleteComponent: deleteComponent, targetDestroyed: targetDestroyed);
    }

    public bool IsMarkedBy(EntityUid target, YautjaMarkKind kind, EntityUid hunter)
    {
        return TryComp(target, out YautjaMarkComponent? mark) &&
               mark.Marks.TryGetValue(kind, out var markedHunter) &&
               markedHunter == hunter;
    }

    public bool TryGetMarkOwner(EntityUid target, YautjaMarkKind kind, out EntityUid hunter)
    {
        hunter = default;
        return TryComp(target, out YautjaMarkComponent? mark) &&
               mark.Marks.TryGetValue(kind, out hunter);
    }

    public string? GetMarkReason(EntityUid target, YautjaMarkKind kind)
    {
        return TryComp(target, out YautjaMarkComponent? mark) &&
               mark.Reasons.TryGetValue(kind, out var reason)
            ? reason
            : null;
    }

    public bool TryClearMark(EntityUid target, YautjaMarkKind kind, EntityUid? hunter = null, bool showPreyRemoved = false)
    {
        if (_net.IsClient ||
            !TryComp(target, out YautjaMarkComponent? mark) ||
            !mark.Marks.TryGetValue(kind, out var markedHunter) ||
            hunter is { } requiredHunter && markedHunter != requiredHunter)
        {
            return false;
        }

        return TryRemoveMark((target, mark), kind, markedHunter, out _, requiredHunter: hunter, showPreyRemoved: showPreyRemoved);
    }

    private bool TryRemoveMark(
        EntityUid target,
        YautjaMarkKind kind,
        EntityUid actor,
        out EntityUid markedHunter,
        EntityUid? requiredHunter = null,
        bool raiseAttempt = true,
        bool deleteComponent = true,
        bool cleanupOnly = false,
        bool targetDestroyed = false,
        bool showPreyRemoved = false)
    {
        markedHunter = default;
        return TryComp(target, out YautjaMarkComponent? mark) &&
               TryRemoveMark((target, mark), kind, actor, out markedHunter, requiredHunter, raiseAttempt, deleteComponent, cleanupOnly, targetDestroyed, showPreyRemoved);
    }

    private bool TryRemoveMark(
        Entity<YautjaMarkComponent> target,
        YautjaMarkKind kind,
        EntityUid actor,
        out EntityUid markedHunter,
        EntityUid? requiredHunter = null,
        bool raiseAttempt = true,
        bool deleteComponent = true,
        bool cleanupOnly = false,
        bool targetDestroyed = false,
        bool showPreyRemoved = false)
    {
        markedHunter = default;
        if (!target.Comp.Marks.TryGetValue(kind, out markedHunter) ||
            requiredHunter is { } required && markedHunter != required)
        {
            return false;
        }

        if (raiseAttempt &&
            IsHonorOrDishonorMark(kind) &&
            actor != markedHunter &&
            !Deleted(markedHunter))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-unmark-not-owner"), actor, actor, PopupType.SmallCaution);
            return false;
        }

        if (raiseAttempt)
        {
            var attempt = new YautjaMarkRemoveAttemptEvent(actor, target.Owner, kind);
            RaiseLocalEvent(target.Owner, ref attempt);
            if (attempt.Cancelled)
                return false;
        }

        target.Comp.Marks.Remove(kind);
        target.Comp.Reasons.Remove(kind);
        if (target.Comp.Marks.Count == 0)
        {
            if (deleteComponent)
                RemCompDeferred<YautjaMarkComponent>(target.Owner);
        }
        else
        {
            Dirty(target.Owner, target.Comp);
        }

        if (targetDestroyed && kind == YautjaMarkKind.Prey)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-mark-prey-destroyed"), markedHunter, markedHunter, PopupType.MediumCaution);

        if (showPreyRemoved && kind == YautjaMarkKind.Prey)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-mark-prey-removed", ("target", target.Owner)),
                actor,
                actor,
                PopupType.Medium);
        }

        if (raiseAttempt &&
            !cleanupOnly &&
            !targetDestroyed)
        {
            if (IsHonorOrDishonorMark(kind))
                BroadcastHonorTransition(actor, target.Owner, kind, true, null);
            else if (kind == YautjaMarkKind.GearCarrier)
                BroadcastGearCarrierTransition(actor, target.Owner, true);
        }

        var removed = new YautjaMarkRemovedEvent(markedHunter, target.Owner, kind, cleanupOnly, targetDestroyed);
        RaiseLocalEvent(target.Owner, ref removed);

        return true;
    }

    private void UpdateUi(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (_net.IsClient || !CanUsePanel(bracer, user))
            return;

        var entries = new List<YautjaMarkPanelEntry>();
        var coords = _transform.GetMapCoordinates(user);
        var targets = _lookup.GetEntitiesInRange(coords, 7f);

        foreach (var target in targets)
        {
            if (!CanMarkTarget(user, target, bracer.Comp, YautjaMarkKind.Prey, false) &&
                !CanMarkTarget(user, target, bracer.Comp, YautjaMarkKind.Thrall, false) &&
                !CanMarkTarget(user, target, bracer.Comp, YautjaMarkKind.Student, false))
                continue;

            var marks = TryComp(target, out YautjaMarkComponent? mark)
                ? new List<YautjaMarkKind>(mark.Marks.Keys)
                : new List<YautjaMarkKind>();

            entries.Add(new YautjaMarkPanelEntry(GetNetEntity(target), Name(target), HasComp<XenoComponent>(target), marks));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        _ui.SetUiState(bracer.Owner, YautjaMarkUIKey.Key, new YautjaMarkPanelState(entries));
    }

    public static string GetMarkName(YautjaMarkKind kind)
    {
        return kind switch
        {
            YautjaMarkKind.Prey => "cmu-yautja-mark-prey",
            YautjaMarkKind.Honored => "cmu-yautja-mark-honored",
            YautjaMarkKind.Dishonored => "cmu-yautja-mark-dishonored",
            YautjaMarkKind.GearCarrier => "cmu-yautja-mark-gear-carrier",
            YautjaMarkKind.Thrall => "cmu-yautja-mark-thrall",
            YautjaMarkKind.Student => "cmu-yautja-mark-student",
            YautjaMarkKind.Blooded => "cmu-yautja-mark-blooded",
            _ => "cmu-yautja-mark-unknown",
        };
    }

    private static bool IsHonorOrDishonorMark(YautjaMarkKind kind)
    {
        return kind is YautjaMarkKind.Honored or YautjaMarkKind.Dishonored;
    }

    private void BroadcastHonorTransition(
        EntityUid hunter,
        EntityUid target,
        YautjaMarkKind kind,
        bool removed,
        string? reason)
    {
        var key = (kind, removed) switch
        {
            (YautjaMarkKind.Honored, false) => "cmu-yautja-mark-honored-broadcast",
            (YautjaMarkKind.Honored, true) => "cmu-yautja-unmark-honored-broadcast",
            (YautjaMarkKind.Dishonored, false) => "cmu-yautja-mark-dishonored-broadcast",
            (YautjaMarkKind.Dishonored, true) => "cmu-yautja-unmark-dishonored-broadcast",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(key))
            return;

        var message = Loc.GetString(
            key,
            ("hunter", Name(hunter)),
            ("target", Name(target)),
            ("reason", reason ?? string.Empty));

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            if (!Deleted(yautja))
                _popup.PopupEntity(message, yautja, yautja, PopupType.Medium);
        }
    }

    private void BroadcastGearCarrierTransition(EntityUid hunter, EntityUid target, bool removed)
    {
        var message = Loc.GetString(
            removed ? "cmu-yautja-unmark-gear-carrier-broadcast" : "cmu-yautja-mark-gear-carrier-broadcast",
            ("hunter", Name(hunter)),
            ("target", Name(target)));

        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            if (!Deleted(yautja))
                _popup.PopupEntity(message, yautja, yautja, PopupType.Medium);
        }
    }
}
