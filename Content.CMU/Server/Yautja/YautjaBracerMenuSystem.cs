using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared.Actions;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaBracerMenuSystem : EntitySystem
{
    private const int TrackerDirections = 6;
    private const int MaxTrackedGear = 12;
    private const float TrackerGroupPrecision = 1f;
    private const float FullCircle = MathF.PI * 2f;
    private static readonly TimeSpan TrackerRefreshEvery = TimeSpan.FromSeconds(1);

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaBracerUtilitySystem _utility = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaSelfDestructSystem _selfDestruct = default!;
    [Dependency] private YautjaThrallSystem _thralls = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private TimeSpan _nextTrackerRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaOpenBracerMenuActionEvent>(OnOpenMenu);

        Subs.BuiEvents<YautjaBracerComponent>(YautjaBracerUIKey.Key, subs =>
        {
            subs.Event<YautjaBracerPanelRefreshMsg>(OnRefresh);
            subs.Event<YautjaBracerPanelCommandMsg>(OnCommand);
        });
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextTrackerRefresh)
            return;

        _nextTrackerRefresh = _timing.CurTime + TrackerRefreshEvery;

        var query = EntityQueryEnumerator<YautjaBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (!_ui.IsUiOpen(uid, YautjaBracerUIKey.Key))
                continue;

            foreach (var actor in _ui.GetActors(uid, YautjaBracerUIKey.Key))
                UpdateUi((uid, bracer), actor, false);
        }
    }

    private void OnOpenMenu(Entity<YautjaBracerComponent> ent, ref YautjaOpenBracerMenuActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!CanUseMenu(ent, args.Performer))
            return;

        _ui.TryOpenUi(ent.Owner, YautjaBracerUIKey.Key, args.Performer);
        UpdateUi(ent, args.Performer);
    }

    private void OnRefresh(Entity<YautjaBracerComponent> ent, ref YautjaBracerPanelRefreshMsg args)
    {
        if (!CanUseMenu(ent, args.Actor))
            return;

        UpdateUi(ent, args.Actor);
    }

    private void OnCommand(Entity<YautjaBracerComponent> ent, ref YautjaBracerPanelCommandMsg args)
    {
        if (!CanUseMenu(ent, args.Actor))
            return;

        switch (args.Command)
        {
            case YautjaBracerPanelCommand.OpenMarks:
                _marks.TryOpenMarkPanel(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.LinkThrallBracer:
                _thralls.TryLinkThrallBracer(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.OpenThrallTransmission:
                _thralls.TryOpenMasterThrallTransmission(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.StunThrall:
                _thralls.TryStunLinkedThrall(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleThrallSelfDestruct:
                _thralls.TryToggleLinkedThrallSelfDestruct(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleThrallBracerLock:
                _thralls.TryToggleLinkedThrallBracerLock(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.OpenTranslator:
                _utility.TryOpenTranslator(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleBracerLock:
                _utility.TryToggleWornBracerLock(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleBracerIdChip:
                _utility.TryToggleIdChip(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.CreateStabilisingCrystal:
                _utility.TryCreateStabilisingCrystal(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.CreateHumanStabilisingCrystal:
                _utility.TryCreateHumanStabilisingCrystal(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.CreateHealingCapsule:
                _utility.TryCreateHealingCapsule(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleSelfDestruct:
                if (ent.Comp.SelfDestructArmed)
                    _selfDestruct.TryCancelSelfDestruct(ent, args.Actor);
                else
                    _selfDestruct.TryArmSelfDestruct(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.RefreshTracker:
                break;
        }

        UpdateUi(ent, args.Actor);
    }

    private void UpdateUi(Entity<YautjaBracerComponent> bracer, EntityUid actor, bool popup = true)
    {
        if (!CanUseMenu(bracer, actor, popup))
            return;

        _thralls.TryGetMasterThrallStatus(
            actor,
            out var thrallName,
            out var thrallLinked,
            out var thrallSelfDestructArmed,
            out var thrallBracerLocked);

        var state = new YautjaBracerPanelState(
            (int) bracer.Comp.Charge,
            (int) bracer.Comp.MaxCharge,
            bracer.Comp.Locked,
            bracer.Comp.IdChipDeployed,
            bracer.Comp.SelfDestructArmed,
            thrallName,
            thrallLinked,
            thrallSelfDestructArmed,
            thrallBracerLocked,
            BuildTracker(actor, bracer.Owner));

        _ui.SetUiState(bracer.Owner, YautjaBracerUIKey.Key, state);
    }

    private List<YautjaGearTrackerEntry> BuildTracker(EntityUid user, EntityUid bracer)
    {
        var origin = _transform.GetMapCoordinates(user);
        var groups = new Dictionary<(int X, int Y), TrackerSignalGroup>();
        var query = EntityQueryEnumerator<YautjaTechItemComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid == bracer ||
                Deleted(uid) ||
                !HasComp<ItemComponent>(uid) ||
                ShouldHideFromTracker(uid))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(uid);
            if (coords.MapId != origin.MapId)
                continue;

            var offset = coords.Position - origin.Position;
            var distance = MathF.Ceiling(offset.Length());
            var angle = GetBearingRadians(offset);
            var direction = GetDirection(angle);
            var bearing = (int) MathF.Round(angle / FullCircle * 360f);
            if (bearing == 360)
                bearing = 0;

            var key = GetTrackerGroupKey(coords.Position);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new TrackerSignalGroup((byte) direction, (int) distance, bearing);
                groups.Add(key, group);
            }

            group.Names.Add(Name(uid));
            if (distance < group.Distance)
                group.SetNearest((byte) direction, (int) distance, bearing);
        }

        var tracked = new List<YautjaGearTrackerEntry>(groups.Count);
        foreach (var group in groups.Values)
        {
            group.Names.Sort(StringComparer.OrdinalIgnoreCase);
            tracked.Add(new YautjaGearTrackerEntry(
                string.Join(", ", group.Names),
                group.Direction,
                group.Distance,
                group.Bearing,
                group.Names.Count));
        }

        tracked.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        if (tracked.Count > MaxTrackedGear)
            tracked.RemoveRange(MaxTrackedGear, tracked.Count - MaxTrackedGear);

        return tracked;
    }

    private bool ShouldHideFromTracker(EntityUid uid)
    {
        return IsStoredInsideYautjaGearContainer(uid) || IsOnYautja(uid);
    }

    private bool IsStoredInsideYautjaGearContainer(EntityUid uid)
    {
        if (!TryComp(uid, out YautjaStoredGearComponent? stored) ||
            stored.Bracer is not { } bracer ||
            Deleted(bracer) ||
            !HasComp<YautjaGearContainerComponent>(bracer))
        {
            return false;
        }

        var current = uid;
        for (var i = 0; i < 32; i++)
        {
            if (!_containers.TryGetContainingContainer((current, null, null), out var container))
                return false;

            if (container.Owner == bracer)
                return true;

            current = container.Owner;
        }

        return false;
    }

    private bool IsOnYautja(EntityUid uid)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var current = uid;
        for (var i = 0; i < 32; i++)
        {
            if (HasComp<YautjaComponent>(current))
                return true;

            if (TryComp(current, out YautjaBracerComponent? bracer) &&
                bracer.User is { } bracerUser &&
                HasComp<YautjaComponent>(bracerUser))
            {
                return true;
            }

            if (_containers.TryGetContainingContainer((current, null, null), out var containing))
            {
                current = containing.Owner;
                continue;
            }

            if (!xformQuery.TryComp(current, out var xform))
                return false;

            if (_containers.TryGetOuterContainer(current, xform, out var container))
            {
                current = container.Owner;
                continue;
            }

            var parent = xform.ParentUid;
            if (parent == current || parent == EntityUid.Invalid || Deleted(parent))
                return false;

            current = parent;
        }

        return false;
    }

    private static (int X, int Y) GetTrackerGroupKey(Vector2 position)
    {
        return (
            (int) MathF.Round(position.X / TrackerGroupPrecision),
            (int) MathF.Round(position.Y / TrackerGroupPrecision));
    }

    private static float GetBearingRadians(Vector2 offset)
    {
        if (offset.LengthSquared() <= 0.001f)
            return 0f;

        var angle = MathF.Atan2(offset.X, offset.Y);
        if (angle < 0)
            angle += FullCircle;

        return angle;
    }

    private static int GetDirection(float angle)
    {
        var sector = FullCircle / TrackerDirections;
        return (int) MathF.Floor((angle + sector / 2f) / sector) % TrackerDirections;
    }

    private bool CanUseMenu(Entity<YautjaBracerComponent> bracer, EntityUid user, bool popup = true)
    {
        if (bracer.Comp.User == user &&
            _power.TryGetWornBracer(user, out var worn) &&
            worn.Owner == bracer.Owner)
        {
            return true;
        }

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-must-be-worn"), user, user, PopupType.SmallCaution);

        return false;
    }

    private sealed partial class TrackerSignalGroup(byte direction, int distance, int bearing)
    {
        public readonly List<string> Names = new();
        public byte Direction { get; private set; } = direction;
        public int Distance { get; private set; } = distance;
        public int Bearing { get; private set; } = bearing;

        public void SetNearest(byte direction, int distance, int bearing)
        {
            Direction = direction;
            Distance = distance;
            Bearing = bearing;
        }
    }
}
