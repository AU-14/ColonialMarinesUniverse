using System.Numerics;
using Content.Server.Shuttles.Events;
using Content.Server._RMC14.Shuttles;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Dropship.MultiDeck;

/// <summary>Ground equipment, boarding ramp and cockpit access for the UD6.</summary>
public sealed partial class MohawkSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MultiDeckDropshipSystem _multiDeck = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private static readonly SoundSpecifier RampSound = new SoundPathSpecifier("/Audio/CMU14/Dropships/Mohawk/omaha_ramp.ogg");
    private static readonly SoundSpecifier HatchSound = new SoundPathSpecifier("/Audio/CMU14/Dropships/Mohawk/nightcustard_motor_whirring.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<MohawkControlComponent, InteractHandEvent>(OnControl);
        SubscribeLocalEvent<MohawkRampSegmentComponent, MapInitEvent>(OnRampMarkerInit);
        SubscribeLocalEvent<MohawkRampEdgingComponent, MapInitEvent>(OnRampEdgingInit);
        SubscribeLocalEvent<MohawkMechanismsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MohawkMechanismsComponent, BeforeFTLStartedEvent>(OnDeparting);
        SubscribeLocalEvent<MohawkMechanismsComponent, FTLCompletedEvent>(OnLanded);
        SubscribeLocalEvent<MohawkMechanismsComponent, DropshipDoorControlEvent>(OnDoorControl);
        SubscribeLocalEvent<MohawkMechanismsComponent, DropshipHijackFlightEvent>(OnHijackFlight);
        InitializeControls();
    }

    private void OnRampMarkerInit(Entity<MohawkRampSegmentComponent> marker, ref MapInitEvent args)
    {
        if (!marker.Comp.Lower && Transform(marker).GridUid is { } grid &&
            TryComp<MohawkMechanismsComponent>(grid, out var mechanisms))
            mechanisms.CabinRampMarkers[marker] = Transform(marker).LocalPosition;
    }

    private void OnShutdown(Entity<MohawkMechanismsComponent> ship, ref ComponentShutdown args)
    {
        // Open ramp cells have no tile and their markers may be parented to the
        // map. They still belong to the ship and must not survive its deletion.
        foreach (var marker in ship.Comp.CabinRampMarkers.Keys)
        {
            if (!TerminatingOrDeleted(marker))
                QueueDel(marker);
        }
        foreach (var edging in ship.Comp.CabinRampEdging.Keys)
        {
            if (!TerminatingOrDeleted(edging))
                QueueDel(edging);
        }
    }

    private void OnRampEdgingInit(Entity<MohawkRampEdgingComponent> edging, ref MapInitEvent args)
    {
        if (Transform(edging).GridUid is { } grid && TryComp<MohawkMechanismsComponent>(grid, out var mechanisms))
            mechanisms.CabinRampEdging[edging] = Transform(edging).LocalPosition;
    }

    private bool CanDeploy(EntityUid ship)
        => TryComp<DropshipComponent>(ship, out var dropship) && !dropship.Crashed &&
           !HasComp<DropshipTacticalHoverComponent>(ship) &&
           (!TryComp<FTLComponent>(ship, out var flight) || flight.State is FTLState.Available or FTLState.Cooldown);

    private void OnDoorControl(Entity<MohawkMechanismsComponent> ship, ref DropshipDoorControlEvent args)
    {
        if (args.Location is DoorLocation.None or DoorLocation.Aft)
            SetRampDeployed(ship, args.Locked is { } locked ? !locked : !ship.Comp.RampDeployed);
    }

    private void OnControl(Entity<MohawkControlComponent> control, ref InteractHandEvent args)
    {
        if (args.Handled || !_dropships.TryGetGridDropship(control, out var ship) ||
            !TryComp<MohawkMechanismsComponent>(ship, out var mechanisms))
            return;

        args.Handled = true;
        if (HasComp<XenoComponent>(args.User))
        {
            TrySabotage(control, args.User);
            return;
        }
        if (mechanisms.BrokenControls.Contains(control.Comp.Group))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-controls-broken"), control, args.User);
            return;
        }
        if (!CanDeploy(ship))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-flight-interlock"), control, args.User);
            return;
        }

        switch (control.Comp.Group)
        {
            case MohawkControlGroup.Ramp:
                SetRampDeployed(ship, !mechanisms.RampDeployed);
                break;
            case MohawkControlGroup.Hatch:
                SetHatchDeployed(ship, !mechanisms.HatchDeployed);
                break;
            case MohawkControlGroup.Port:
            case MohawkControlGroup.Starboard:
                var location = control.Comp.Group == MohawkControlGroup.Port ? DoorLocation.Port : DoorLocation.Starboard;
                foreach (var uid in GetShipEntities(ship))
                {
                    if (TryComp<DoorComponent>(uid, out var door) && door.Location == location)
                    {
                        if (TryComp<DoorBoltComponent>(uid, out var bolts))
                            _doors.SetBoltsDown((uid, bolts), false);
                        _doors.TryToggleDoor(uid, door, args.User);
                    }
                }
                break;
        }
    }

    /// <summary>Retracts before the primary grid enters hyperspace, including cancelled flights.</summary>
    private void OnDeparting(Entity<MohawkMechanismsComponent> ship, ref BeforeFTLStartedEvent args)
    {
        SetRampDeployed(ship, false, true);
        SetHatchDeployed(ship, false, true);
    }

    private void OnHijackFlight(Entity<MohawkMechanismsComponent> ship, ref DropshipHijackFlightEvent args)
    {
        // Restore the cabin floor and bring ramp riders inside before removing
        // the lower deck. Neither boarding device can be used on the wreck.
        SetRampDeployed(ship, false, true);
        SetHatchDeployed(ship, false, true);
        ship.Comp.BoardingDisabled = true;
        _multiDeck.RemoveSecondaryDecks(ship);
        if (TryComp<DropshipComponent>(ship, out var dropship))
            _dropships.SetDropshipCrashed((ship, dropship), true);
    }

    private void OnLanded(Entity<MohawkMechanismsComponent> ship, ref FTLCompletedEvent args)
    {
        if (!TryComp<MultiDeckDropshipComponent>(ship, out var decks) ||
            !_multiDeck.Synchronize((ship, decks)))
            return;

        var victims = new HashSet<EntityUid>();
        foreach (var uid in GetShipEntities(ship))
        {
            if (!HasComp<MohawkLandingCrushComponent>(uid))
                continue;
            foreach (var victim in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 0.65f))
            {
                // Riders already on a deck must not be crushed by their own ship.
                if (!_dropships.TryGetGridDropship(victim, out var riding) || riding.Owner != ship.Owner)
                    victims.Add(victim);
            }
        }
        foreach (var victim in victims)
            _damage.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Blunt"] = 1000 } }, true, origin: ship);
    }

    public bool SetHatchDeployed(EntityUid ship, bool deployed, bool force = false)
    {
        if (!TryComp<MohawkMechanismsComponent>(ship, out var mechanisms) ||
            (deployed && mechanisms.BoardingDisabled) ||
            (!force && (mechanisms.HatchDeployed == deployed || HasComp<MohawkHatchMovingComponent>(ship))) ||
            (deployed && !force && !CanDeploy(ship)))
            return false;

        if (!force)
        {
            var moving = EnsureComp<MohawkHatchMovingComponent>(ship);
            moving.Deploying = deployed;
            moving.FinishAt = _timing.CurTime + TimeSpan.FromSeconds(1.8);
            foreach (var uid in GetShipEntities(ship))
            {
                if (HasComp<MohawkHatchComponent>(uid))
                    _appearance.SetData(uid, MohawkVisuals.HatchState, deployed ? "hatch-opening" : "hatch-closing");
                // Retracting ladders immediately stops new climbing attempts.
                if (!deployed && (HasComp<MohawkHatchComponent>(uid) || HasComp<MohawkLowerLadderComponent>(uid)))
                    RemComp<CMUZLevelLadderComponent>(uid);
            }
            _audio.PlayPvs(HatchSound, ship);
            return true;
        }

        RemComp<MohawkHatchMovingComponent>(ship);

        foreach (var uid in GetShipEntities(ship))
        {
            var upper = HasComp<MohawkHatchComponent>(uid);
            if (!upper && !HasComp<MohawkLowerLadderComponent>(uid))
                continue;

            if (deployed)
            {
                var ladder = EnsureComp<CMUZLevelLadderComponent>(uid);
                ladder.Offset = upper ? -1 : 1;
                ladder.StartSound = new SoundPathSpecifier("/Audio/CMU14/Dropships/Mohawk/mountain852_climbing_ladder_initial.ogg");
                ladder.FinishSound = new SoundPathSpecifier("/Audio/CMU14/Dropships/Mohawk/mountain852_climbing_ladder_human_after.ogg");
                Dirty(uid, ladder);
            }
            else
            {
                RemComp<CMUZLevelLadderComponent>(uid);
            }
            _appearance.SetData(uid, MohawkVisuals.Deployed, deployed);
            if (upper)
                _appearance.SetData(uid, MohawkVisuals.HatchState, deployed ? "hatch-open" : "ladder-hatch-closed");
        }
        if (mechanisms.HatchDeployed != deployed && deployed)
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/CMU14/Dropships/Mohawk/freesoundstock_step_ladder.ogg"), ship);
        mechanisms.HatchDeployed = deployed;
        return true;
    }

    public bool SetRampDeployed(EntityUid ship, bool deployed, bool force = false)
    {
        if (!TryComp<MohawkMechanismsComponent>(ship, out var mechanisms) ||
            (deployed && mechanisms.BoardingDisabled) ||
            (!force && (mechanisms.RampDeployed == deployed || HasComp<MohawkRampMovingComponent>(ship))) ||
            (!force && mechanisms.BrokenControls.Contains(MohawkControlGroup.Ramp)) ||
            (deployed && !force && !CanDeploy(ship)) ||
            !TryComp<MultiDeckDropshipComponent>(ship, out var decks) || !decks.Initialized)
            return false;

        if (!force)
        {
            var moving = EnsureComp<MohawkRampMovingComponent>(ship);
            moving.Deploying = deployed;
            moving.Step = 0;
            moving.NextStep = _timing.CurTime + TimeSpan.FromSeconds(2.5);
            ApplyRampGeometry(ship, 2);
            _audio.PlayPvs(RampSound, ship);
            return true;
        }

        RemComp<MohawkRampMovingComponent>(ship);
        ApplyRampGeometry(ship, deployed ? 5 : 0);
        mechanisms.RampDeployed = deployed;
        var changed = new DropshipBoardingChangedEvent();
        RaiseLocalEvent(ship, ref changed);
        return true;
    }

    public override void Update(float frameTime)
    {
        var ramps = EntityQueryEnumerator<MohawkRampMovingComponent>();
        while (ramps.MoveNext(out var ship, out var moving))
        {
            if (_timing.CurTime < moving.NextStep)
                continue;
            moving.Step++;
            if (moving.Step >= 2)
            {
                SetRampDeployed(ship, moving.Deploying, true);
                continue;
            }
            ApplyRampGeometry(ship, moving.Deploying ? 3 : 1);
            moving.NextStep += TimeSpan.FromSeconds(2.5);
        }

        var hatches = EntityQueryEnumerator<MohawkHatchMovingComponent>();
        while (hatches.MoveNext(out var ship, out var moving))
        {
            if (_timing.CurTime >= moving.FinishAt)
                SetHatchDeployed(ship, moving.Deploying, true);
        }
    }

    private void ApplyRampGeometry(EntityUid ship, int deployedStages)
    {
        var mechanisms = Comp<MohawkMechanismsComponent>(ship);
        var parts = GetShipEntities(ship);
        var descending = new List<(EntityUid Rider, Vector2 Position)>();
        foreach (var marker in mechanisms.CabinRampMarkers.Keys)
        {
            if (!parts.Contains(marker))
                parts.Add(marker);
        }
        // Restore the cabin floor before moving occupants onto it. Open markers
        // can be parented to the map, so child enumeration order is not reliable.
        foreach (var (marker, position) in mechanisms.CabinRampMarkers)
        {
            if (!TryComp<MohawkRampSegmentComponent>(marker, out var segment) || segment.Stage >= 4 ||
                !TryComp<MapGridComponent>(ship, out var cabin))
                continue;

            var coordinates = new EntityCoordinates(ship, position);
            var deployed = segment.Stage < deployedStages;
            if (deployed && !segment.Deployed)
            {
                foreach (var rider in GetRampOccupants(marker))
                {
                    var local = Vector2.Transform(_transform.GetWorldPosition(rider), _transform.GetInvWorldMatrix(ship));
                    descending.Add((rider, local + mechanisms.LoweredRampOffset));
                }
            }
            var indices = _map.TileIndicesFor(ship, cabin, coordinates);
            segment.ClosedTile ??= _map.GetTileRef(ship, cabin, indices).Tile;
            _map.SetTile(ship, cabin, indices, deployed ? Tile.Empty : segment.ClosedTile.Value);
            _transform.SetCoordinates((marker, Transform(marker), MetaData(marker)), coordinates);
            segment.Deployed = deployed;
        }
        // Removing a cabin floor tile reparents even non-traversing decorations
        // to the map. Keep the opening's edging attached after all tiles change.
        foreach (var (edging, position) in mechanisms.CabinRampEdging)
        {
            if (!TerminatingOrDeleted(edging))
                _transform.SetCoordinates((edging, Transform(edging), MetaData(edging)), new EntityCoordinates(ship, position));
        }
        foreach (var uid in parts)
        {
            if (!TryComp<MohawkRampSegmentComponent>(uid, out var segment))
                continue;
            var xform = Transform(uid);
            var grid = segment.Lower ? xform.GridUid : ship;
            if (grid is not { } owner || !TryComp<MapGridComponent>(owner, out var gridComp))
                continue;

            var deployed = segment.Stage < deployedStages;

            if (segment.Lower)
            {
                if (deployed)
                {
                    EnsureComp<BlockWeedsComponent>(uid);
                    if (!segment.Deployed)
                    {
                        foreach (var victim in GetRampOccupants(uid))
                        {
                            _damage.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Blunt"] = 40 } }, origin: ship);
                            _stun.TryKnockdown(victim.Owner, TimeSpan.FromSeconds(5), false);
                            var angle = _random.NextFloat() * MathF.Tau;
                            _throwing.TryThrow(victim.Owner, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4, baseThrowSpeed: 3f);
                        }
                    }
                    // Only ramp-10/11/12 rise between decks, using the ordinary
                    // multi-Z stair profile. The two outer rows are flat ground.
                    if (segment.HeightCurve is { } curve)
                    {
                        var support = EnsureComp<CMUZLevelHighGroundComponent>(uid);
                        support.HeightCurve = new(curve);
                        support.Stick = true;
                        support.AllowVehicles = false;
                        Dirty(uid, support);
                    }
                }
                else
                {
                    RemComp<BlockWeedsComponent>(uid);
                    // A ramp can overlap a landing pad; standing on it does not
                    // necessarily reparent the occupant to the ship's lower grid.
                    if (segment.Deployed && segment.Stage != 4)
                    {
                        foreach (var rider in GetRampOccupants(uid))
                        {
                            var position = _transform.GetWorldPosition(rider);
                            var local = Vector2.Transform(position, _transform.GetInvWorldMatrix(ship)) - mechanisms.LoweredRampOffset;
                            _transform.SetCoordinates((rider, Transform(rider), MetaData(rider)), new EntityCoordinates(ship, local));
                            if (TryComp<CMUZPhysicsComponent>(rider, out var physics))
                            {
                                _zLevels.SetZLocalPosition((rider, physics), 0f);
                                _zLevels.SetZVelocity((rider, physics), 0f);
                            }
                        }
                    }
                    RemComp<CMUZLevelHighGroundComponent>(uid);
                }
                if (segment.Stage == 4)
                {
                    _physics.SetCanCollide(uid, deployed);
                    if (deployed)
                        EnsureComp<ZLevelWallSupportComponent>(uid);
                    else
                        RemComp<ZLevelWallSupportComponent>(uid);
                }
                _appearance.SetData(uid, MohawkVisuals.Deployed, deployed);
                segment.Deployed = deployed;
            }
        }
        // Move riders only after the support exists and its underneath has been
        // checked. They ride the lowering surface instead of being crushed by it.
        if (TryComp<MultiDeckDropshipComponent>(ship, out var assembly) && assembly.Decks.TryGetValue(-1, out var lower))
        {
            foreach (var (rider, position) in descending)
            {
                _transform.SetCoordinates((rider, Transform(rider), MetaData(rider)), new EntityCoordinates(lower, position));
                if (TryComp<CMUZPhysicsComponent>(rider, out var physics))
                {
                    // Let the same support calculation used by walking place
                    // riders on the visible surface; the old end caps reach ground.
                    var distance = _zLevels.DistanceToGround((rider, physics), out _);
                    _zLevels.SetZLocalPosition((rider, physics), MathF.Max(0, physics.LocalPosition - distance));
                    _zLevels.SetZVelocity((rider, physics), 0f);
                }
            }
        }
    }

    private IEnumerable<Entity<MobStateComponent>> GetRampOccupants(EntityUid segment)
    {
        var inverse = _transform.GetInvWorldMatrix(segment);
        foreach (var occupant in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(segment).Coordinates, 0.8f))
        {
            var local = Vector2.Transform(_transform.GetWorldPosition(occupant), inverse);
            // Query whole tiles, including corners, without hitting someone in
            // the next row merely because their collision shape overlaps it.
            if (local.X >= -0.5f && local.X < 0.5f && local.Y >= -0.5f && local.Y < 0.5f)
                yield return occupant;
        }
    }

    private List<EntityUid> GetShipEntities(EntityUid ship)
    {
        var result = new List<EntityUid>();
        AddChildren(ship, result);
        if (TryComp<MultiDeckDropshipComponent>(ship, out var decks))
        {
            foreach (var (_, grid) in decks.Decks)
            {
                if (!TerminatingOrDeleted(grid))
                    AddChildren(grid, result);
            }
        }
        return result;
    }

    private void AddChildren(EntityUid grid, List<EntityUid> result)
    {
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var uid))
            result.Add(uid);
    }
}
