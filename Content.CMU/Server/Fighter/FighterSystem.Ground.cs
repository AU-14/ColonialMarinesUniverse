using System.Numerics;
using Content.Server.CMU14.Round.Objectives;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared._RMC14.Vehicle.Supply;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private SharedPhysicsSystem _groundPhysics = default!;
    [Dependency] private EntityLookupSystem _groundLookup = default!;
    [Dependency] private SharedInteractionSystem _groundInteraction = default!;
    [Dependency] private ObjectiveControlSystem _objectives = default!;
    [Dependency] private FixtureSystem _groundFixtures = default!;
    private readonly HashSet<EntityUid> _groundBlockers = [];

    private void InitializeGround()
    {
        SubscribeLocalEvent<FighterGroundComponent, MapInitEvent>(OnGroundInit);
        SubscribeLocalEvent<FighterGroundComponent, ActivateInWorldEvent>(OnGroundActivate);
        SubscribeLocalEvent<FighterGroundComponent, VehicleSupplyStorageAttemptEvent>(OnGroundStorage);
        SubscribeLocalEvent<FighterGroundComponent, ComponentShutdown>(OnGroundShutdown);
        SubscribeLocalEvent<FighterGroundComponent, PreventCollideEvent>(OnGroundPreventCollide);
        SubscribeLocalEvent<FighterSeatComponent, UnstrapAttemptEvent>(OnGroundUnstrapAttempt);
        SubscribeLocalEvent<FighterSeatComponent, StrapAttemptEvent>(OnGroundStrapAttempt);
    }

    private void OnGroundStrapAttempt(Entity<FighterSeatComponent> seat, ref StrapAttemptEvent args)
    {
        if (seat.Comp.Aircraft is not { } aircraft || !TryComp(aircraft, out FighterAircraftComponent? flight)) return;
        if (!CanBoardAircraft(args.Buckle.Owner, (aircraft, flight)))
        {
            args.Cancelled = true;
            if (args.Popup && args.User is { } user)
                _popup.PopupEntity(Loc.GetString("cmu-fighter-iff-denied"), seat, user);
            return;
        }
        if (flight.GroundEntity is { } hull && TryComp(hull, out FighterGroundComponent? ground) &&
            ground.State != FighterGroundState.Grounded && !ground.SwappingSeat)
            args.Cancelled = true;
    }

    private void OnGroundInit(Entity<FighterGroundComponent> ground, ref MapInitEvent args)
    {
        if (ground.Comp.Aircraft != null || Transform(ground).MapUid is not { } terrain) return;
        // Ship-side delivery still flies over the active battlefield, not the carrier map.
        if (_objectives.GetPlanetMapId() is { } planet && planet != MapId.Nullspace && _map.MapExists(planet))
            terrain = _map.GetMap(planet);
        var aircraft = CreateAircraft(terrain, _transform.GetWorldPosition(ground));
        aircraft.Comp.GroundEntity = ground;
        ground.Comp.Aircraft = aircraft;
        ground.Comp.FrontSeat = aircraft.Comp.FrontSeat;
        ground.Comp.RearSeat = aircraft.Comp.RearSeat;
        ground.Comp.Canopy = aircraft.Comp.Canopy;
        if (TryComp(ground, out FighterIFFComponent? iff))
            SetEquipmentFaction((ground, iff), iff.Faction ?? _iff.GetSiteFaction(ground));
        ground.Comp.LaunchCoordinates = Transform(ground).Coordinates;
        ground.Comp.LaunchRotation = _transform.GetWorldRotation(ground);
        SetGroundState(ground, aircraft, FighterGroundState.Grounded);
        aircraft.Comp.Height = aircraft.Comp.Speed = 0;
        MoveFighterMounts(ground, aircraft, true);
        LoadStartingAmmo(ground.Comp, aircraft);
        MoveFighterCrew(ground, aircraft, true);
    }

    private void OnGroundActivate(Entity<FighterGroundComponent> ground, ref ActivateInWorldEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        if (!TryBoardGround(args.User, ground))
            _popup.PopupEntity(Loc.GetString("cmu-fighter-board-blocked"), ground, args.User);
    }

    public bool TryBoardGround(EntityUid user, EntityUid groundUid)
    {
        if (!TryComp(groundUid, out FighterGroundComponent? ground) || ground.State != FighterGroundState.Grounded ||
            ground.Aircraft is not { } uid || !TryComp(uid, out FighterAircraftComponent? flight) ||
            !_groundInteraction.InRangeUnobstructed(user, groundUid)) return false;
        return Board(user, (uid, flight)) || Board(user, (uid, flight), true);
    }

    private void OnGroundStorage(Entity<FighterGroundComponent> ground, ref VehicleSupplyStorageAttemptEvent args)
    {
        if (ground.Comp.State != FighterGroundState.Grounded) { args.Cancelled = true; return; }
        if (ground.Comp.Aircraft is not { } uid || !TryComp(uid, out FighterAircraftComponent? flight)) return;
        foreach (var seat in new[] { flight.FrontSeat, flight.RearSeat })
            if (CompOrNull<FighterSeatComponent>(seat)?.Occupant != null) { args.Cancelled = true; return; }
        var map = Transform(uid).MapUid;
        var occupants = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (occupants.MoveNext(out _, out _, out var xform))
            if (xform.MapUid == map) { args.Cancelled = true; return; }
    }

    private void OnGroundUnstrapAttempt(Entity<FighterSeatComponent> seat, ref UnstrapAttemptEvent args)
    {
        if (seat.Comp.Aircraft is { } uid && TryComp(uid, out FighterAircraftComponent? flight) &&
            flight.GroundEntity is { } hull && TryComp(hull, out FighterGroundComponent? ground) &&
            ground.State != FighterGroundState.Grounded && !ground.SwappingSeat && args.User != null)
        {
            args.Cancelled = true;
            // Only the occupant can arm their ejection handle. External unbuckle
            // attempts must not eject another player or silently open a warning.
            if (args.User == seat.Comp.Occupant) TryRequestEjection(args.User);
        }
    }

    private void OnGroundPreventCollide(Entity<FighterGroundComponent> ground, ref PreventCollideEvent args)
    {
        if (IsGroundCrew(ground, args.OtherEntity)) args.Cancelled = true;
    }

    private bool IsGroundCrew(EntityUid hull, EntityUid entity) =>
        TryComp(entity, out BuckleComponent? buckle) && buckle.BuckledTo is { } seat && Transform(seat).ParentUid == hull;

    private void AttachCrewPhysics(Entity<FighterSeatComponent> seat, EntityUid crew)
    {
        if (!TryComp(crew, out PhysicsComponent? body)) return;
        seat.Comp.OccupantBodyType = body.BodyType;
        seat.Comp.OccupantCanCollide = body.CanCollide;
        // The hull already moves the seat and occupant through their transforms.
        // Simulating the occupant separately can shift them off the buckle socket,
        // causing SharedBuckleSystem's position check to forcibly unbuckle them.
        _groundPhysics.ResetDynamics(crew, body);
        _groundPhysics.SetCanCollide(crew, false, body: body);
        _groundPhysics.SetBodyType(crew, BodyType.Static, body: body);
        if (TryComp(crew, out CMUZPhysicsComponent? zPhysics))
        {
            _zLevels.SetZLocalPosition((crew, zPhysics), 0);
            _zLevels.SetZVelocity((crew, zPhysics), 0);
        }
    }

    private void RestoreCrewPhysics(Entity<FighterSeatComponent> seat)
    {
        if (seat.Comp.OccupantBodyType is not { } type) return;
        seat.Comp.OccupantBodyType = null;
        if (seat.Comp.Occupant is not { } crew || TerminatingOrDeleted(crew) ||
            !TryComp(crew, out PhysicsComponent? body)) return;
        _groundPhysics.ResetDynamics(crew, body);
        _groundPhysics.SetBodyType(crew, type, body: body);
        // Restoring CanCollide alone leaves a previously static body asleep.
        // Wake it after detaching so ordinary movement and contacts resume.
        if (seat.Comp.OccupantCanCollide)
            _groundPhysics.WakeBody(crew, body: body);
        else
            _groundPhysics.SetCanCollide(crew, false, body: body);
        if (TryComp(crew, out CMUZPhysicsComponent? zPhysics))
            _zLevels.WakeZPhysics((crew, zPhysics));
    }

    private void MoveFighterCrew(Entity<FighterGroundComponent> ground, Entity<FighterAircraftComponent> aircraft, bool toGround)
    {
        foreach (var uid in new[] { aircraft.Comp.FrontSeat, aircraft.Comp.RearSeat, aircraft.Comp.Canopy })
        {
            if (uid is not { } part || TerminatingOrDeleted(part)) continue;
            if (TryComp(part, out FighterSeatComponent? seat) && seat.Occupant is { } crew)
                _crewPulling.TryStopAllPullsFromAndOn(crew);
            var position = part == aircraft.Comp.FrontSeat ? new Vector2(.5f, 4.2f) :
                part == aircraft.Comp.RearSeat ? new Vector2(.5f, 2.1f) : new Vector2(.5f, 0);
            if (toGround) position = -(position - new Vector2(.5f, 0)) * FighterGroundComponent.AttachmentScale;
            _transform.SetCoordinates(part, new EntityCoordinates(toGround ? ground.Owner : aircraft.Owner, position));
            _transform.SetLocalRotation(part, toGround ? Angle.Zero : new Angle(Math.PI));
            if (TryComp(part, out PhysicsComponent? body))
            {
                _groundPhysics.SetBodyType(part, BodyType.Static, body: body);
                _groundPhysics.SetCanCollide(part, !toGround, body: body);
            }
        }
    }

    private void ExitGroundSeat(Entity<FighterSeatComponent> seat, EntityUid occupant)
    {
        if (seat.Comp.Aircraft is not { } uid || !TryComp(uid, out FighterAircraftComponent? flight) ||
            flight.GroundEntity is not { } hull || !TryComp(hull, out FighterGroundComponent? ground) ||
            ground.SwappingSeat || ground.State != FighterGroundState.Grounded || Transform(hull).MapUid == null) return;
        var xform = Transform(hull);
        var exit = xform.LocalRotation.RotateVec(new Vector2(2.5f * FighterGroundComponent.SizeMultiplier, 0));
        _transform.SetCoordinates(occupant, xform.Coordinates.Offset(exit));
    }

    private void OnGroundShutdown(Entity<FighterGroundComponent> ground, ref ComponentShutdown args)
    {
        FinishVtolEffects(ground, FighterVtolOutcome.Aborted);
        if (ground.Comp.Aircraft is not { } uid || TerminatingOrDeleted(uid)) return;
        var cockpitMap = Transform(uid).MapUid;
        var recovery = Transform(ground).MapUid != null ? Transform(ground).Coordinates : ground.Comp.LaunchCoordinates;
        if (recovery is { } destination && !TerminatingOrDeleted(destination.EntityId) && _transform.GetMap(destination) != null)
        {
            ground.Comp.SwappingSeat = true;
            if (TryComp(uid, out FighterAircraftComponent? aircraft))
                foreach (var seat in new[] { aircraft.FrontSeat, aircraft.RearSeat })
                    if (CompOrNull<FighterSeatComponent>(seat)?.Occupant is { } crew)
                    {
                        _buckle.Unbuckle(crew, null);
                        _transform.SetCoordinates(crew, destination.Offset(new Vector2(2.5f, 0)));
                    }
            var occupants = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
            while (occupants.MoveNext(out var mob, out _, out var xform))
            {
                if (xform.MapUid != cockpitMap) continue;
                _buckle.Unbuckle(mob, null);
                _transform.SetCoordinates(mob, destination.Offset(new Vector2(2.5f, 0)));
            }
            if (cockpitMap is { } map && !TerminatingOrDeleted(map)) QueueDel(map);
        }
        // If the launch map was removed, retain the occupied cockpit for round cleanup
        // rather than deleting its passengers with the ground representation.
    }

    public bool TryTakeoff(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !seat.Comp.Pilot || aircraft.Comp.ForcedRetreat ||
            aircraft.Comp.GroundEntity is not { } hull || !TryComp(hull, out FighterGroundComponent? ground) ||
            ground.State != FighterGroundState.Grounded) return false;
        var coordinates = Transform(hull).Coordinates;
        if (!GroundSiteClear(hull, coordinates))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fighter-takeoff-blocked"), seat.Owner, user);
            return false;
        }
        ground.LaunchCoordinates = coordinates;
        ground.TaxiPad = null;
        ground.LaunchRotation = _transform.GetWorldRotation(hull);
        _groundPhysics.SetLinearVelocity(hull, Vector2.Zero);
        _groundPhysics.SetBodyType(hull, BodyType.Static);
        SetGroundState((hull, ground), aircraft, FighterGroundState.TakingOff, ground.TakeoffTime);
        BeginVtolEffects((hull, ground), aircraft, false);
        return true;
    }

    public bool TryReturnToGround(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            aircraft.Comp.GroundEntity == null || aircraft.Comp.GroundState != FighterGroundState.Airborne) return false;
        ReturnToGround(aircraft);
        return true;
    }

    private void ReturnToGround(Entity<FighterAircraftComponent> aircraft)
    {
        if (aircraft.Comp.GroundEntity is not { } hull || !TryComp(hull, out FighterGroundComponent? ground)) return;
        FighterFlight.Abort(aircraft.Comp);
        SetGroundState((hull, ground), aircraft, FighterGroundState.Returning);
        foreach (var seatUid in new[] { aircraft.Comp.FrontSeat, aircraft.Comp.RearSeat })
            if (seatUid is { } uid && TryComp(uid, out FighterSeatComponent? seat))
            { CancelQueuedFire((uid, seat)); CancelLaserLock((uid, seat)); ClearLaser((uid, seat)); }
    }

    private void SetGroundState(Entity<FighterGroundComponent> ground, Entity<FighterAircraftComponent> aircraft,
        FighterGroundState state, TimeSpan duration = default)
    {
        ground.Comp.State = aircraft.Comp.GroundState = state;
        SetWingFixtures(ground, state != FighterGroundState.Grounded);
        ground.Comp.TaxiPad = null;
        StopTaxi(ground);
        UpdateTaxiOperator(ground, aircraft);
        if (state is FighterGroundState.Grounded or FighterGroundState.TakingOff)
            MoveFighterMounts(ground, aircraft, true);
        aircraft.Comp.RecoveryHandoff = false;
        ground.Comp.StartedAt = _timing.CurTime;
        ground.Comp.EndsAt = _timing.CurTime + duration;
        aircraft.Comp.GroundStateStartedAt = ground.Comp.StartedAt;
        aircraft.Comp.GroundStateEndsAt = ground.Comp.EndsAt;
        // Changing maps is a new view. Taxiing, banking and running a pass are not.
        if (state is FighterGroundState.Airborne or FighterGroundState.Landing)
            foreach (var seatUid in new[] { aircraft.Comp.FrontSeat, aircraft.Comp.RearSeat })
                if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? camera))
                {
                    camera.SensorFocus = camera.SensorLock = null;
                    camera.Target = null;
                    camera.TargetPosition = null;
                    Dirty(seat, camera);
                }
        if (state == FighterGroundState.Grounded)
            foreach (var seatUid in new[] { aircraft.Comp.FrontSeat, aircraft.Comp.RearSeat })
                if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? crew))
                { crew.EjectConfirmUntil = TimeSpan.Zero; Dirty(seat, crew); }
        Dirty(ground);
        Dirty(aircraft);
    }

    private void MoveFighterMounts(Entity<FighterGroundComponent> ground, Entity<FighterAircraftComponent> aircraft, bool toGround)
    {
        if (!TryComp(aircraft, out FighterWeaponsComponent? weapons)) return;
        for (var i = 0; i < weapons.Hardpoints.Count; i++)
        {
            var cannon = i == FighterWeaponsComponent.GauSlot;
            var x = cannon ? .5f : .5f + (i < 3 ? -1 : 1) * (2.5f + i % 3 * 1.8f);
            var position = new Vector2(x, cannon ? 5.5f : -1.5f - i % 3 * .7f);
            if (toGround)
            {
                position = -(position - new Vector2(.5f, 0)) * FighterGroundComponent.AttachmentScale;
                if (!cannon && ground.Comp.State == FighterGroundState.Grounded)
                    position.X = (i < 3 ? 1 : -1) * (.48f + i % 3 * .065f) * FighterGroundComponent.SizeMultiplier;
            }
            _transform.SetCoordinates(weapons.Hardpoints[i], new EntityCoordinates(toGround ? ground.Owner : aircraft.Owner, position));
            _transform.SetLocalRotation(weapons.Hardpoints[i], toGround ? new Angle(Math.PI) : Angle.Zero);
            // Reparenting unanchors structures and makes them dynamic. Pylons must
            // follow the airframe without colliding with it or drifting off the wings.
            _groundPhysics.SetBodyType(weapons.Hardpoints[i], BodyType.Static);
            _groundPhysics.SetCanCollide(weapons.Hardpoints[i], !toGround);
        }
    }

    private void UpdateGround(Entity<FighterAircraftComponent> aircraft, float frameTime)
    {
        var a = aircraft.Comp;
        if (a.GroundEntity is not { } hull || !TryComp(hull, out FighterGroundComponent? component)) return;
        var ground = new Entity<FighterGroundComponent>(hull, component);
        var now = _timing.CurTime;
        if (a.GroundState == FighterGroundState.Airborne && (a.ForcedRetreat || !HasCombatPilot(a))) ReturnToGround(aircraft);
        if (a.GroundState == FighterGroundState.Grounded)
        {
            if (Transform(hull).MapUid == null) return; // In lift storage.
            UpdateTaxi(ground, aircraft, frameTime);
            var heading = Math.PI - _transform.GetWorldRotation(hull).Theta;
            a.Position = _transform.GetWorldPosition(hull);
            a.Heading = (float) heading;
            a.Height = a.Speed = 0;
            return;
        }
        if (a.GroundState == FighterGroundState.Returning && a.Phase == FighterPhase.Holding && now >= component.EndsAt)
        {
            if (component.LaunchCoordinates is not { } launchCoordinates || !GroundSiteClear(hull, launchCoordinates))
            { a.RecoveryHandoff = false; component.EndsAt = now + TimeSpan.FromSeconds(2); return; }
            if (!a.RecoveryHandoff)
            {
                SetGroundState(ground, aircraft, FighterGroundState.Returning, TimeSpan.FromSeconds(1.2));
                a.RecoveryHandoff = true;
                Dirty(aircraft);
                return;
            }
            _transform.SetCoordinates(hull, launchCoordinates);
            _transform.SetWorldRotation(hull, component.LaunchRotation);
            MoveFighterCrew(ground, aircraft, true);
            MoveFighterMounts(ground, aircraft, true);
            SetGroundState(ground, aircraft, FighterGroundState.Landing, component.LandingTime);
            BeginVtolEffects(ground, aircraft, true);
        }
        if (a.GroundState is not (FighterGroundState.TakingOff or FighterGroundState.Landing)) return;
        var duration = Math.Max(.01, (component.EndsAt - component.StartedAt).TotalSeconds);
        var progress = (float) Math.Clamp((now - component.StartedAt).TotalSeconds / duration, 0, 1);
        a.Position = _transform.GetWorldPosition(hull);
        a.Heading = (float) (Math.PI - component.LaunchRotation.Theta);
        a.Height = FighterVtol.Lift(progress, a.GroundState == FighterGroundState.Landing) * 100;
        a.Altitude = FighterAltitude.Low;
        a.Speed = 0;
        if (now < component.EndsAt) return;
        if (a.GroundState == FighterGroundState.TakingOff)
        {
            if (!HasCombatPilot(a) || !GroundSiteClear(hull, Transform(hull).Coordinates))
            {
                FinishVtolEffects(ground, FighterVtolOutcome.Aborted);
                SetGroundState(ground, aircraft, FighterGroundState.Grounded);
                _groundPhysics.SetBodyType(hull, BodyType.Dynamic);
                a.Height = 0;
                return;
            }
            FinishVtolEffects(ground, FighterVtolOutcome.Departed);
            MoveFighterMounts(ground, aircraft, false);
            MoveFighterCrew(ground, aircraft, false);
            _transform.SetParent(hull, EntityUid.Invalid);
            a.Position = FighterFlight.HoldingPoint(a);
            a.Height = a.TargetHeight;
            a.Speed = a.TargetSpeed;
            SetGroundState(ground, aircraft, FighterGroundState.Airborne);
        }
        else if (component.LaunchCoordinates is { } landingCoordinates && GroundSiteClear(hull, landingCoordinates))
        {
            FinishVtolEffects(ground, FighterVtolOutcome.Touchdown);
            a.Height = a.Speed = 0;
            MoveFighterMounts(ground, aircraft, true);
            _groundPhysics.SetBodyType(hull, BodyType.Dynamic);
            SetGroundState(ground, aircraft, FighterGroundState.Grounded);
        }
        else
        {
            FinishVtolEffects(ground, FighterVtolOutcome.Aborted);
            MoveFighterCrew(ground, aircraft, false);
            MoveFighterMounts(ground, aircraft, false);
            _transform.SetParent(hull, EntityUid.Invalid);
            a.Position = FighterFlight.HoldingPoint(a);
            a.Height = a.TargetHeight;
            SetGroundState(ground, aircraft, FighterGroundState.Returning, TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>Rechecked at both ends of the transition; occupants and obstructions never get crushed by landing.</summary>
    public bool GroundSiteClear(EntityUid hull, EntityCoordinates coordinates)
    {
        if (TerminatingOrDeleted(coordinates.EntityId) || _transform.GetMap(coordinates) is not { } map) return false;
        if (!CanUseGroundSite(hull, coordinates)) return false;
        var world = _transform.ToMapCoordinates(coordinates);
        var rotation = TryComp(hull, out FighterGroundComponent? ground) && ground.State is FighterGroundState.Airborne or FighterGroundState.Returning
            ? ground.LaunchRotation : _transform.GetWorldRotation(hull);
        var lifts = EntityQueryEnumerator<VehicleSupplyLiftComponent, TransformComponent>();
        while (lifts.MoveNext(out var liftUid, out var lift, out var liftTransform))
            if (liftTransform.MapID == world.MapId && Vector2.DistanceSquared(_transform.GetWorldPosition(liftUid), world.Position) <= lift.Radius * lift.Radius &&
                (lift.Mode != VehicleSupplyLiftMode.Raised || lift.Busy)) return false;
        for (var x = -2; x <= 2; x++)
        for (var y = -3; y <= 3; y++)
        {
            var point = world.Position + rotation.RotateVec(new Vector2(x, y * .85f) * FighterGroundComponent.SizeMultiplier);
            if (!HasGroundTile(map, point)) return false;
            var upper = map;
            while (TryComp(upper, out CMUZLevelMapComponent? level) && level.MapAbove is { } above)
            {
                if (HasGroundTile(above, point)) return false;
                upper = above;
            }
            _areas.CanOrbitalBombard(new EntityCoordinates(map, point), out var roofed);
            if (roofed) return false;
        }
        _groundBlockers.Clear();
        var c = (float) Math.Abs(Math.Cos(rotation.Theta));
        var s = (float) Math.Abs(Math.Sin(rotation.Theta));
        var extent = new Vector2(c * 2.2f + s * 2.8f, s * 2.2f + c * 2.8f) * FighterGroundComponent.SizeMultiplier;
        _groundLookup.GetEntitiesIntersecting(world.MapId, new Box2(world.Position - extent, world.Position + extent),
            _groundBlockers, LookupFlags.Uncontained);
        foreach (var entity in _groundBlockers)
        {
            if (entity == hull || Transform(entity).ParentUid == hull || IsGroundCrew(hull, entity)) continue;
            // Characters use soft fixtures. Include those and prone bodies as well as hard obstacles.
            if (HasComp<MobStateComponent>(entity)) return false;
            if (!TryComp(entity, out FixturesComponent? fixtures) || !TryComp(entity, out PhysicsComponent? body) || !body.CanCollide) continue;
            foreach (var fixture in fixtures.Fixtures.Values)
                if (fixture.Hard && (fixture.CollisionLayer & (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable |
                    CollisionGroup.HighImpassable | CollisionGroup.MobLayer | CollisionGroup.MobMask)) != 0) return false;
        }
        return true;
    }

    private void SetWingFixtures(Entity<FighterGroundComponent> ground, bool extended)
    {
        const int layer = (int) CollisionGroup.LargeMobLayer;
        const int mask = (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable |
            CollisionGroup.LowImpassable | CollisionGroup.BarricadeImpassable | CollisionGroup.LargeMobLayer);
        foreach (var id in new[] { "wings", "foldedWings" })
        {
            if (_groundFixtures.GetFixtureOrNull(ground, id) is not { } fixture) continue;
            var active = id == "wings" ? extended : !extended;
            _groundPhysics.SetHard(ground, fixture, active);
            _groundPhysics.SetCollisionLayer(ground, id, fixture, active ? layer : 0);
            _groundPhysics.SetCollisionMask(ground, id, fixture, active ? mask : 0);
        }
    }

    private bool HasGroundTile(EntityUid map, Vector2 point)
    {
        var coordinates = new MapCoordinates(point, Transform(map).MapID);
        if (TryComp(map, out MapGridComponent? mapGrid) && !_map.GetTileRef((map, mapGrid), coordinates).Tile.IsEmpty) return true;
        foreach (var grid in _map.GetAllGrids(coordinates.MapId))
            if (!_map.GetTileRef(grid, coordinates).Tile.IsEmpty) return true;
        return false;
    }
}
