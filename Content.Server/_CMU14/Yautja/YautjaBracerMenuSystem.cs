using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Rules;
using Content.Shared.Actions;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaBracerMenuSystem : EntitySystem
{
    private const int TrackerDirections = 6;
    private const int MaxTrackedGear = 12;
    private const float TrackerGroupPrecision = 1f;
    private const float FullCircle = MathF.PI * 2f;
    private const int MaxClosestSignatureDistance = 900;
    private const string DeadYautjaBioSignatureName = "cmu-yautja-tracker-dead-signature";
    private static readonly TimeSpan TrackerRefreshEvery = TimeSpan.FromSeconds(1);

    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaBracerUtilitySystem _utility = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaSelfDestructSystem _selfDestruct = default!;
    [Dependency] private YautjaThrallSystem _thralls = default!;
    [Dependency] private YautjaYoungbloodSystem _youngbloods = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private TimeSpan _nextTrackerRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaOpenBracerMenuActionEvent>(OnOpenMenu);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaTrackGearActionEvent>(OnTrackGear);

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
        TryOpenTracker(ent, args.Performer);
    }

    public bool TryOpenTracker(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseMenu(bracer, user))
            return false;

        _ui.TryOpenUi(bracer.Owner, YautjaBracerUIKey.Key, user);
        UpdateUi(bracer, user);
        return true;
    }

    private void OnTrackGear(Entity<YautjaBracerComponent> ent, ref YautjaTrackGearActionEvent args)
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
            case YautjaBracerPanelCommand.RemoteExecuteYoungblood:
                _youngbloods.TryOpenRemoteExecution(ent, args.Actor);
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
            case YautjaBracerPanelCommand.CreateHuntingTrap:
                _utility.TryCreateHuntingTrap(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleSelfDestruct:
                _selfDestruct.TryOpenSelfDestructDialog(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ChangeExplosionType:
                _utility.TryChangeExplosionType(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.RemoveBracerAttachments:
                if (TryComp(ent.Owner, out YautjaGearContainerComponent? gearContainer))
                    EntityManager.System<YautjaAttachmentSystem>().TryRemoveBracerAttachments((ent.Owner, gearContainer), args.Actor);
                break;
            case YautjaBracerPanelCommand.CreateHealingCapsule:
                _utility.TryCreateHealingCapsule(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.AddTrackedItem:
                _utility.TryAddTrackedItem(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.RemoveTrackedItem:
                _utility.TryRemoveTrackedItem(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleBracerName:
                _utility.TryToggleBracerName(ent, args.Actor);
                break;
            case YautjaBracerPanelCommand.ToggleBracerNotificationSound:
                _utility.TryToggleNotificationSound(ent, args.Actor);
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

        var tracker = BuildTracker(actor, bracer.Owner);
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
            tracker.Readout,
            tracker.Entries);

        _ui.SetUiState(bracer.Owner, YautjaBracerUIKey.Key, state);
    }

    private TrackerBuildResult BuildTracker(EntityUid user, EntityUid bracer)
    {
        var origin = _transform.GetMapCoordinates(user);
        var readout = new TrackerReadoutBuilder();
        var groups = new Dictionary<(int X, int Y), TrackerSignalGroup>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (uid == bracer ||
                Deleted(uid))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(uid);
            if (coords.MapId == MapId.Nullspace)
                continue;

            if (HasComp<YautjaComponent>(uid) &&
                _mobState.IsDead(uid))
            {
                readout.AddDead(GetTrackerLevelBucket(uid, xform, coords, origin));
                if (coords.MapId == origin.MapId)
                {
                    AddTrackerSignal(groups, origin, coords, Loc.GetString(DeadYautjaBioSignatureName), readout, null);
                }

                continue;
            }

            if (!HasComp<ItemComponent>(uid) ||
                !IsTrackedItem(uid) ||
                xform.Anchored ||
                ShouldHideFromTracker(uid))
            {
                continue;
            }

            readout.AddGear(GetTrackerLevelBucket(uid, xform, coords, origin));
            if (coords.MapId == origin.MapId)
                AddTrackerSignal(groups, origin, coords, Name(uid), readout, Name(uid));
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

        return new TrackerBuildResult(tracked, readout.Build());
    }

    private void AddTrackerSignal(
        Dictionary<(int X, int Y), TrackerSignalGroup> groups,
        MapCoordinates origin,
        MapCoordinates coords,
        string name,
        TrackerReadoutBuilder readout,
        string? closestName)
    {
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

        group.Names.Add(name);
        if (distance < group.Distance)
            group.SetNearest((byte) direction, (int) distance, bearing);

        readout.TrySetClosest((int) distance, (byte) direction, bearing, closestName, GetTrackerAreaName(coords));
    }

    private bool IsTrackedItem(EntityUid uid)
    {
        return HasComp<YautjaTrackedItemComponent>(uid);
    }

    private string GetTrackerAreaName(MapCoordinates coords)
    {
        return _areas.TryGetArea(coords, out _, out var areaPrototype)
            ? areaPrototype.Name
            : Loc.GetString("rmc-tacmap-alert-no-area");
    }

    private TrackerLevelBucket GetTrackerLevelBucket(EntityUid uid, TransformComponent xform, MapCoordinates coords, MapCoordinates origin)
    {
        if (_areas.TryGetArea(coords, out var area, out var areaPrototype))
        {
            if (IsCmss13MainshipTrackerArea(area.Value.Comp.PowerNet, areaPrototype.ID))
                return TrackerLevelBucket.Orbit;

            if (IsCmss13LowOrbitTrackerArea(area.Value.Comp.PowerNet, areaPrototype.ID))
                return TrackerLevelBucket.LowOrbit;

            return TrackerLevelBucket.HuntingGrounds;
        }

        if (IsPlanetOrGroundMap(xform))
            return TrackerLevelBucket.HuntingGrounds;

        return coords.MapId == origin.MapId
            ? TrackerLevelBucket.HuntingGrounds
            : TrackerLevelBucket.LowOrbit;
    }

    private bool IsPlanetOrGroundMap(TransformComponent xform)
    {
        return xform.GridUid is { } grid && HasComp<RMCPlanetComponent>(grid) ||
               xform.MapUid is { } map && HasComp<RMCPlanetComponent>(map);
    }

    public static bool IsCmss13MainshipTrackerArea(string? powerNet, string areaPrototypeId)
    {
        return IsPowerNet(powerNet, "almayer") ||
               IsPowerNet(powerNet, "warship") ||
               IsPowerNet(powerNet, "bush") ||
               areaPrototypeId.Contains("Almayer", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCmss13LowOrbitTrackerArea(string? powerNet, string areaPrototypeId)
    {
        return IsPowerNet(powerNet, "ert") ||
               IsPowerNet(powerNet, "fax") ||
               IsPowerNet(powerNet, "faxexterior") ||
               areaPrototypeId.Contains("Space", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPowerNet(string? powerNet, string expected)
    {
        return string.Equals(powerNet, expected, StringComparison.OrdinalIgnoreCase);
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
        if (IsWornBy(bracer, user))
        {
            return true;
        }

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-must-be-worn"), user, user, PopupType.SmallCaution);

        return false;
    }

    private bool IsWornBy(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return bracer.Comp.User == user &&
               _power.TryGetWornBracer(user, out var worn) &&
               worn.Owner == bracer.Owner;
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

    private sealed class TrackerBuildResult(List<YautjaGearTrackerEntry> entries, YautjaTrackerReadout readout)
    {
        public readonly List<YautjaGearTrackerEntry> Entries = entries;
        public readonly YautjaTrackerReadout Readout = readout;
    }

    private sealed class TrackerReadoutBuilder
    {
        private int _closestDistance = int.MaxValue;
        private byte _closestDirection;
        private int _closestBearing;
        private string? _closestName;
        private string? _closestArea;

        public int DeadHuntingGrounds;
        public int DeadOrbit;
        public int DeadLowOrbit;
        public int GearHuntingGrounds;
        public int GearOrbit;
        public int GearLowOrbit;

        public void AddDead(TrackerLevelBucket bucket)
        {
            switch (bucket)
            {
                case TrackerLevelBucket.HuntingGrounds:
                    DeadHuntingGrounds++;
                    break;
                case TrackerLevelBucket.Orbit:
                    DeadOrbit++;
                    break;
                case TrackerLevelBucket.LowOrbit:
                    DeadLowOrbit++;
                    break;
            }
        }

        public void AddGear(TrackerLevelBucket bucket)
        {
            switch (bucket)
            {
                case TrackerLevelBucket.HuntingGrounds:
                    GearHuntingGrounds++;
                    break;
                case TrackerLevelBucket.Orbit:
                    GearOrbit++;
                    break;
                case TrackerLevelBucket.LowOrbit:
                    GearLowOrbit++;
                    break;
            }
        }

        public void TrySetClosest(int distance, byte direction, int bearing, string? name, string area)
        {
            if (distance >= MaxClosestSignatureDistance)
                return;

            if (distance > _closestDistance ||
                distance == _closestDistance && (_closestName != null || name == null))
            {
                return;
            }

            _closestDistance = distance;
            _closestDirection = direction;
            _closestBearing = bearing;
            _closestName = name;
            _closestArea = area;
        }

        public YautjaTrackerReadout Build()
        {
            var closestPresent = _closestDistance < MaxClosestSignatureDistance;
            return new YautjaTrackerReadout(
                DeadHuntingGrounds,
                DeadOrbit,
                DeadLowOrbit,
                GearHuntingGrounds,
                GearOrbit,
                GearLowOrbit,
                closestPresent,
                _closestName,
                closestPresent ? _closestDistance : 0,
                _closestDirection,
                _closestBearing,
                _closestArea);
        }
    }

    private enum TrackerLevelBucket : byte
    {
        HuntingGrounds,
        Orbit,
        LowOrbit,
    }
}
