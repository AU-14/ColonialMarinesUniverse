using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaMarkSystem : EntitySystem
{
    private const int MaxReasonLength = 120;

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
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
        SubscribeLocalEvent<YautjaMarkComponent, MobStateChangedEvent>(OnMarkedMobStateChanged);

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

    private void OnMarkedMobStateChanged(Entity<YautjaMarkComponent> ent, ref MobStateChangedEvent args)
    {
        if (_net.IsClient || args.NewMobState != MobState.Dead)
            return;

        RemCompDeferred<YautjaMarkComponent>(ent);
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

        if (!CanUsePanel(ent, args.Actor) || !CanMarkTarget(args.Actor, target.Value, ent.Comp, args.Kind, false))
            return;

        var attempt = new YautjaMarkRemoveAttemptEvent(args.Actor, target.Value, args.Kind);
        RaiseLocalEvent(target.Value, ref attempt);
        if (attempt.Cancelled)
            return;

        if (!TryComp(target.Value, out YautjaMarkComponent? mark) || !mark.Marks.Remove(args.Kind))
            return;

        if (mark.Marks.Count == 0)
            RemCompDeferred<YautjaMarkComponent>(target.Value);
        else
            Dirty(target.Value, mark);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):actor} removed Yautja mark {args.Kind} from {ToPrettyString(target.Value):target}");

        var removed = new YautjaMarkRemovedEvent(args.Actor, target.Value, args.Kind);
        RaiseLocalEvent(target.Value, ref removed);

        UpdateUi(ent, args.Actor);
    }

    public bool TryMark(Entity<YautjaBracerComponent> bracer, EntityUid hunter, EntityUid target, YautjaMarkKind kind, string? reason)
    {
        if (!CanUsePanel(bracer, hunter) || !CanMarkTarget(hunter, target, bracer.Comp, kind, true))
            return false;

        if (kind == YautjaMarkKind.Prey && HunterHasPrey(hunter, target))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-mark-already-hunting"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var mark = EnsureComp<YautjaMarkComponent>(target);
        if (kind == YautjaMarkKind.Prey &&
            mark.Marks.TryGetValue(YautjaMarkKind.Prey, out var existingHunter) &&
            existingHunter != hunter)
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-mark-prey-claimed"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var trimmed = reason?.Trim();
        if (trimmed is { Length: > MaxReasonLength })
            trimmed = trimmed[..MaxReasonLength];

        var attempt = new YautjaMarkAttemptEvent(hunter, target, kind, trimmed);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
        {
            if (mark.Marks.Count == 0)
                RemCompDeferred<YautjaMarkComponent>(target);

            return false;
        }

        mark.Marks[kind] = hunter;
        Dirty(target, mark);
        if (!HasComp<YautjaComponent>(target))
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

        var applied = new YautjaMarkAppliedEvent(hunter, target, kind, trimmed);
        RaiseLocalEvent(target, ref applied);

        return true;
    }

    public void ForceMark(EntityUid hunter, EntityUid target, YautjaMarkKind kind, bool addStatusIcon = true)
    {
        if (_net.IsClient)
            return;

        var mark = EnsureComp<YautjaMarkComponent>(target);
        mark.Marks[kind] = hunter;
        Dirty(target, mark);

        if (addStatusIcon)
            EnsureComp<StatusIconComponent>(target);
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
        return kind switch
        {
            YautjaMarkKind.Thrall => humanoid && !HasComp<YautjaComponent>(target),
            YautjaMarkKind.Blooded => humanoid && !HasComp<YautjaComponent>(target),
            YautjaMarkKind.Honored => humanoid,
            YautjaMarkKind.GearCarrier => humanoid,
            YautjaMarkKind.Student => HasComp<YautjaComponent>(target),
            _ => humanoid || xeno,
        };
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
            var removed = false;
            var toRemove = new List<YautjaMarkKind>();
            foreach (var (kind, markedHunter) in mark.Marks)
            {
                if (markedHunter == hunter)
                    toRemove.Add(kind);
            }

            foreach (var kind in toRemove)
                removed |= mark.Marks.Remove(kind);

            if (!removed)
                continue;

            if (mark.Marks.Count == 0)
                RemCompDeferred<YautjaMarkComponent>(uid);
            else
                Dirty(uid, mark);
        }
    }

    public bool IsMarkedBy(EntityUid target, YautjaMarkKind kind, EntityUid hunter)
    {
        return TryComp(target, out YautjaMarkComponent? mark) &&
               mark.Marks.TryGetValue(kind, out var markedHunter) &&
               markedHunter == hunter;
    }

    public bool TryClearMark(EntityUid target, YautjaMarkKind kind, EntityUid? hunter = null)
    {
        if (_net.IsClient ||
            !TryComp(target, out YautjaMarkComponent? mark) ||
            !mark.Marks.TryGetValue(kind, out var markedHunter) ||
            hunter is { } requiredHunter && markedHunter != requiredHunter)
        {
            return false;
        }

        var attempt = new YautjaMarkRemoveAttemptEvent(markedHunter, target, kind);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
            return false;

        mark.Marks.Remove(kind);
        if (mark.Marks.Count == 0)
            RemCompDeferred<YautjaMarkComponent>(target);
        else
            Dirty(target, mark);

        var removed = new YautjaMarkRemovedEvent(markedHunter, target, kind);
        RaiseLocalEvent(target, ref removed);

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
}
