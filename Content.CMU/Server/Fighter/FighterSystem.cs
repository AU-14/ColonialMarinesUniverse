using System.Numerics;
using Content.Shared._RMC14.Pulling;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Parallax;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private RMCPullingSystem _crewPulling = default!;
    [Dependency] private ViewSubscriberSystem _views = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterSeatComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<FighterSeatComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<FighterSeatComponent, ComponentShutdown>(OnSeatShutdown);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnFighterRoundCleanup);
        SubscribeNetworkEvent<FighterCommandEvent>(OnCommand);
        SubscribeNetworkEvent<FighterPlanEvent>(OnPlan);
        SubscribeNetworkEvent<FighterSettingsEvent>(OnSettings);
        SubscribeNetworkEvent<FighterCoverSectorEvent>(OnCoverSector);
        InitializeDevelopment();
        InitializeWeapons();
        InitializeIFF();
        InitializeGround();
        InitializeSpectators();
        UpdatesBefore.Add(typeof(Content.Shared.Vehicle.GridVehicleMoverSystem));
        SubscribeLocalEvent<FighterManpadComponent, ComponentShutdown>(OnManpadShutdown);
        SubscribeLocalEvent<FighterManpadComponent, FighterManpadAimStoppedEvent>(OnManpadAimStopped);
        SubscribeLocalEvent<FighterBoilerAirDefenseComponent, FighterBoilerAirDefenseStoppedEvent>(OnBoilerStopped);
        SubscribeLocalEvent<FighterAircraftComponent, ComponentShutdown>(OnAircraftShutdown);
    }

    public Entity<FighterAircraftComponent> CreateAircraft(EntityUid terrain, Vector2 home)
    {
        // A station's largest grid can be a roof level. Always locate the ground map first.
        while (TryComp(terrain, out CMUZLevelMapComponent? upper) && upper.Depth > 0 && upper.MapBelow is { } below)
            terrain = below;
        var cockpitMap = _map.CreateMap(out var cockpitId, runMapInit: true);
        var backdrop = EnsureComp<ParallaxComponent>(cockpitMap);
        backdrop.Parallax = "CMUFighterEmpty";
        Dirty(cockpitMap, backdrop);
        var atmosphere = new GasMixture(2500) { Temperature = 293.15f };
        atmosphere.SetMoles(Gas.Oxygen, 21.824879f);
        atmosphere.SetMoles(Gas.Nitrogen, 82.10312f);
        _atmos.SetMapAtmosphere(cockpitMap, false, atmosphere);
        var grid = _map.CreateGridEntity(cockpitId);
        var floor = new Tile(_tiles["RMCFloorVehicleInteriorDarkSterile"].TileId);
        for (var x = -7; x <= 7; x++)
        for (var y = -5; y <= 6; y++)
            _map.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), floor);

        var aircraft = AddComp<FighterAircraftComponent>(grid);
        aircraft.TerrainMap = terrain;
        aircraft.ViewMap = TryComp(terrain, out CMUZLevelMapComponent? level) && level.MapAbove is { } above ? above : terrain;
        aircraft.Home = home;
        var chart = AddComp<FighterChartComponent>(grid);
        BuildChart(terrain, aircraft, chart);
        aircraft.Position = FighterFlight.HoldingPoint(aircraft);
        aircraft.Entry = aircraft.Home + new Vector2(0, aircraft.Battlefield.Height / 2 + 12);
        aircraft.Exit = aircraft.Home - new Vector2(0, aircraft.Battlefield.Height / 2 + 12);
        // Spawn coordinates override prototype rotation, so orient every physical part explicitly.
        var forward = new Angle(Math.PI);
        aircraft.Hull = SpawnAttachedTo("CMUFighterHull", new EntityCoordinates(grid, new Vector2(.5f, 0)), rotation: forward);
        aircraft.Canopy = SpawnAttachedTo("CMUFighterCanopy", new EntityCoordinates(grid, new Vector2(.5f, 0)), rotation: forward);
        SpawnAttachedTo("AU14VehicleRadioSet", new EntityCoordinates(grid, new Vector2(1.5f, 3.5f)));
        // Scale the narrow canopy and its seats together to fit ordinary crew,
        // without changing the players' own sprite scale.
        aircraft.FrontSeat = SpawnAttachedTo("CMUFighterPilotSeat", new EntityCoordinates(grid, new Vector2(.5f, 4.2f)), rotation: forward);
        aircraft.RearSeat = SpawnAttachedTo("CMUFighterObserverSeat", new EntityCoordinates(grid, new Vector2(.5f, 2.1f)), rotation: forward);
        foreach (var seat in new[] { aircraft.FrontSeat.Value, aircraft.RearSeat.Value })
        {
            var component = Comp<FighterSeatComponent>(seat);
            component.Aircraft = grid;
            Dirty(seat, component);
        }
        CreateHardpoints((grid, aircraft));
        AddComp<FighterAirCombatComponent>(grid);
        AddComp<FighterEffectsComponent>(grid);
        Dirty(grid, aircraft);
        Log.Info($"Fighter test cockpit {ToPrettyString(grid.Owner)} ready over {ToPrettyString(terrain)} at {home}.");
        return (grid, aircraft);
    }

    public bool Board(EntityUid player, Entity<FighterAircraftComponent> aircraft, bool rear = false)
    {
        if (!CanBoardAircraft(player, aircraft)) return false;
        var target = rear ? aircraft.Comp.RearSeat : aircraft.Comp.FrontSeat;
        if (target is not { } seat || !TryComp(seat, out FighterSeatComponent? fighterSeat) || fighterSeat.Occupant != null)
            return false;
        var original = Transform(player).Coordinates;
        var rotation = Transform(player).LocalRotation;
        _crewPulling.TryStopAllPullsFromAndOn(player);
        _transform.SetCoordinates(player, Transform(seat).Coordinates);
        if (!_buckle.TryBuckle(player, player, seat, popup: false))
        {
            _transform.SetCoordinates(player, original);
            _transform.SetLocalRotation(player, rotation);
            return false;
        }
        return true;
    }

    private void OnStrapped(Entity<FighterSeatComponent> seat, ref StrappedEvent args)
    {
        if (seat.Comp.Aircraft is not { } aircraftUid || !TryComp(aircraftUid, out FighterAircraftComponent? aircraft))
            return;
        seat.Comp.Occupant = args.Buckle.Owner;
        AttachCrewPhysics(seat, args.Buckle.Owner);
        // Clicking a visible ground seat uses the ordinary buckle system rather
        // than Board(). Both entry paths must establish the aircraft's faction.
        if (TryComp(aircraftUid, out FighterWeaponsComponent? weapons) && weapons.Faction == null)
        {
            weapons.Faction = _iff.GetOperatorFaction(args.Buckle.Owner);
            if (aircraft.GroundEntity is { } hull && TryComp(hull, out FighterIFFComponent? iff))
                SetEquipmentFaction((hull, iff), weapons.Faction);
            Dirty(aircraftUid, weapons);
        }
        seat.Comp.Input = FighterInput.None;
        seat.Comp.SensorFocus = null;
        seat.Comp.LastInput = _timing.CurTime;
        var camera = Spawn("CMUFighterCamera", Transform(seat).Coordinates);
        seat.Comp.Camera = camera;
        _zLevels.EnsureZLevelViewer(camera);
        var exterior = Spawn("CMUFighterCamera", Transform(seat).Coordinates);
        seat.Comp.ExteriorCamera = exterior;
        _zLevels.EnsureZLevelViewer(exterior);
        if (TryComp(args.Buckle.Owner, out ActorComponent? actor))
        {
            _views.AddViewSubscriber(aircraftUid, actor.PlayerSession);
            _views.AddViewSubscriber(camera, actor.PlayerSession);
            _views.AddViewSubscriber(exterior, actor.PlayerSession);
        }
        UpdateCamera(seat, aircraft);
        if (aircraft.GroundEntity is { } ground && TryComp(ground, out FighterGroundComponent? groundComp))
            UpdateTaxiOperator((ground, groundComp), (aircraftUid, aircraft));
        Dirty(seat);
    }

    private void OnUnstrapped(Entity<FighterSeatComponent> seat, ref UnstrappedEvent args)
    {
        ReleaseTaxiOperator(seat);
        ExitGroundSeat(seat, args.Buckle.Owner);
        ClearSeat(seat);
    }
    private void OnSeatShutdown(Entity<FighterSeatComponent> seat, ref ComponentShutdown args) => ClearSeat(seat);

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryGetSeat(ev.Entity, out var seat, out _))
            return;
        if (seat.Comp.Camera is { } camera) _views.AddViewSubscriber(camera, ev.Player);
        if (seat.Comp.Aircraft is { } aircraft) _views.AddViewSubscriber(aircraft, ev.Player);
        if (seat.Comp.ExteriorCamera is { } exterior) _views.AddViewSubscriber(exterior, ev.Player);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        RemoveSpectatorViews(ev.Player);
        if (!TryGetSeat(ev.Entity, out var seat, out _))
            return;
        seat.Comp.Input = FighterInput.None;
        if (seat.Comp.Aircraft is { } aircraft) _views.RemoveViewSubscriber(aircraft, ev.Player);
        CancelQueuedFire(seat);
        CancelLaserLock(seat);
        ClearLaser(seat);
        if (seat.Comp.Camera is { } camera)
            _views.RemoveViewSubscriber(camera, ev.Player);
        if (seat.Comp.ExteriorCamera is { } exterior)
            _views.RemoveViewSubscriber(exterior, ev.Player);
    }

    private void ClearSeat(Entity<FighterSeatComponent> seat)
    {
        ReleaseTaxiOperator(seat);
        RestoreCrewPhysics(seat);
        if (seat.Comp.Aircraft is { } cockpit && seat.Comp.Occupant is { } crew && TryComp(crew, out ActorComponent? crewActor))
            _views.RemoveViewSubscriber(cockpit, crewActor.PlayerSession);
        seat.Comp.EjectConfirmUntil = TimeSpan.Zero;
        CancelQueuedFire(seat);
        CancelLaserLock(seat);
        ClearLaser(seat);
        if (seat.Comp.Pilot && seat.Comp.Aircraft is { } aircraftUid && TryComp(aircraftUid, out FighterAircraftComponent? aircraft))
            FighterFlight.Abort(aircraft);
        if (seat.Comp.Camera is { } camera && !TerminatingOrDeleted(camera))
        {
            if (seat.Comp.Occupant is { } occupant && TryComp(occupant, out ActorComponent? actor))
                _views.RemoveViewSubscriber(camera, actor.PlayerSession);
            QueueDel(camera);
        }
        if (seat.Comp.ExteriorCamera is { } exterior && !TerminatingOrDeleted(exterior))
        {
            if (seat.Comp.Occupant is { } occupant && TryComp(occupant, out ActorComponent? actor))
                _views.RemoveViewSubscriber(exterior, actor.PlayerSession);
            QueueDel(exterior);
        }
        seat.Comp.Camera = null;
        seat.Comp.ExteriorCamera = null;
        seat.Comp.Occupant = null;
        seat.Comp.Input = FighterInput.None;
        seat.Comp.Aim = Vector2.Zero;
        seat.Comp.SensorLock = null;
        seat.Comp.SensorFocus = null;
        seat.Comp.Target = null;
        seat.Comp.TargetPosition = null;
        seat.Comp.CameraControl = false;
        seat.Comp.Zoomed = false;
        if (!TerminatingOrDeleted(seat))
            Dirty(seat);
    }

    private bool TryGetSeat(EntityUid? player, out Entity<FighterSeatComponent> seat, out Entity<FighterAircraftComponent> aircraft)
    {
        seat = default;
        aircraft = default;
        if (player is not { } user || !TryComp(user, out BuckleComponent? buckle) || buckle.BuckledTo is not { } seatUid ||
            !TryComp(seatUid, out FighterSeatComponent? component) || component.Occupant != user ||
            component.Aircraft is not { } aircraftUid || !TryComp(aircraftUid, out FighterAircraftComponent? flight))
            return false;
        seat = (seatUid, component);
        aircraft = (aircraftUid, flight);
        return true;
    }

    private void OnCommand(FighterCommandEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetSeat(args.SenderSession.AttachedEntity, out var seat, out var aircraft) ||
            !FighterFlight.CanControl(seat.Comp.Pilot, ev.Command))
            return;
        // Countermeasures have their own one-attempt guard. A recent camera click
        // must not swallow a pilot's response near the impact deadline.
        if (ev.Command == FighterCommand.Flares)
        {
            TryDeployFlares(seat.Comp.Occupant);
            return;
        }
        if (_timing.CurTime < seat.Comp.NextCommand) return;
        seat.Comp.NextCommand = _timing.CurTime + TimeSpan.FromMilliseconds(150);
        var player = seat.Comp.Occupant!.Value;
        switch (ev.Command)
        {
            case FighterCommand.FormUp:
                TryFormUp(player);
                break;
            case FighterCommand.ConfirmEject:
                TryConfirmEjection(player);
                break;
            case FighterCommand.CancelEject:
                seat.Comp.EjectConfirmUntil = TimeSpan.Zero;
                break;
            case FighterCommand.Takeoff:
                TryTakeoff(player);
                break;
            case FighterCommand.PrepareRun:
                if (!TryPrepareRun(player)) _popup.PopupEntity(Loc.GetString("cmu-fighter-assist-unavailable"), player, player);
                break;
            case FighterCommand.QueueFire:
                TryQueueFire(player);
                break;
            case FighterCommand.Launch:
                if (!FighterFlight.Launch(aircraft.Comp))
                    break;
                if (aircraft.Comp.Hull is { } runningHull)
                    _ambient.SetAmbience(runningHull, true);
                PlayPhaseEffect(aircraft, aircraft.Comp.Phase);
                break;
            case FighterCommand.Ascend:
                aircraft.Comp.TargetHeight = Math.Min(FighterFlight.MaximumHeight, aircraft.Comp.TargetHeight + 350);
                break;
            case FighterCommand.Descend:
                aircraft.Comp.TargetHeight = Math.Max(FighterFlight.MinimumHeight, aircraft.Comp.TargetHeight - 350);
                break;
            case FighterCommand.Return:
                if (aircraft.Comp.GroundEntity != null)
                {
                    TryReturnToGround(player);
                    break;
                }
                CancelQueuedFire(seat);
                var previousPhase = aircraft.Comp.Phase;
                FighterFlight.Abort(aircraft.Comp);
                if (aircraft.Comp.Phase != previousPhase) PlayPhaseEffect(aircraft, aircraft.Comp.Phase);
                break;
            case FighterCommand.Zoom:
                seat.Comp.Zoomed = !seat.Comp.Zoomed;
                break;
            case FighterCommand.Center:
                CancelLaserLock(seat);
                seat.Comp.Aim = Vector2.Zero;
                seat.Comp.SensorFocus = FighterFlight.GroundScene(aircraft.Comp) && aircraft.Comp.GroundEntity is { } centerHull
                    ? _transform.GetWorldPosition(centerHull)
                    : aircraft.Comp.Position + FighterFlight.Forward(aircraft.Comp.Heading) * 14;
                seat.Comp.SensorLock = null;
                seat.Comp.Target = null;
                seat.Comp.TargetPosition = null;
                break;
            case FighterCommand.Lock:
                CancelLaserLock(seat);
                FighterFlight.ToggleSensorLock(aircraft.Comp, seat.Comp);
                break;
            case FighterCommand.Laser:
                if (!TryLase(player, out var laserStatus))
                    _popup.PopupEntity(Loc.GetString("cmu-fighter-fire-" + laserStatus.ToString().ToLowerInvariant()), player, player);
                break;
            case FighterCommand.VisionNormal:
                FighterOptics.TrySetMode(aircraft.Comp, FighterSensorMode.Normal, _timing.CurTime);
                break;
            case FighterCommand.VisionNight:
                FighterOptics.TrySetMode(aircraft.Comp, FighterSensorMode.NightVision, _timing.CurTime);
                break;
            case FighterCommand.VisionThermal:
                if (!FighterOptics.TrySetMode(aircraft.Comp, FighterSensorMode.Thermal, _timing.CurTime))
                    _popup.PopupEntity(Loc.GetString("cmu-fighter-thermal-cooling-popup"), player, player);
                break;
            case FighterCommand.Mark:
                var point = FighterFlight.SensorPosition(aircraft.Comp, seat.Comp);
                if (!FighterFlight.SensorAvailable(aircraft.Comp, point, seat.Comp))
                    _popup.PopupEntity(Loc.GetString("cmu-fighter-standby"), player, player);
                else if (FighterOptics.CloudsBlock(aircraft.Comp, point, _timing.CurTime))
                    _popup.PopupEntity(Loc.GetString("cmu-fighter-obscured"), player, player);
                else
                {
                    aircraft.Comp.Mark = point;
                    aircraft.Comp.MarkAge = 0;
                    aircraft.Comp.MarkPass = aircraft.Comp.PassNumber;
                }
                break;
            case FighterCommand.Fire:
                CancelQueuedFire(seat);
                if (!TryFire(player, out var fireStatus))
                    _popup.PopupEntity(Loc.GetString("cmu-fighter-fire-" + fireStatus.ToString().ToLowerInvariant()), player, player);
                break;
            case FighterCommand.SwapSeat:
                var otherUid = seat.Comp.Pilot ? aircraft.Comp.RearSeat : aircraft.Comp.FrontSeat;
                if (otherUid is { } other && Comp<FighterSeatComponent>(other).Occupant == null)
                {
                    var ground = CompOrNull<FighterGroundComponent>(aircraft.Comp.GroundEntity);
                    if (ground != null) ground.SwappingSeat = true;
                    try
                    {
                        if (_buckle.TryUnbuckle(player, player)) Board(player, aircraft, !seat.Comp.Pilot);
                    }
                    finally { if (ground != null) ground.SwappingSeat = false; }
                }
                break;
            case FighterCommand.TrainingRelease:
                if (FighterFlight.CanRelease(aircraft.Comp, _timing.CurTime))
                    aircraft.Comp.TrainingImpact = aircraft.Comp.Mark;
                break;
            case FighterCommand.LeaveSeat:
                if (!TryRequestEjection(player)) _buckle.TryUnbuckle(player, player);
                break;
        }
        Dirty(aircraft);
        Dirty(seat);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FighterAircraftComponent>();
        while (query.MoveNext(out var uid, out var aircraft))
        {
            UpdateGround((uid, aircraft), frameTime);
            var previousSensorMode = aircraft.SensorMode;
            FighterOptics.Update(aircraft, _timing.CurTime);
            if (previousSensorMode == FighterSensorMode.Thermal && aircraft.SensorMode != previousSensorMode)
                PlayEffect(uid, FighterEffectKind.Overheat, duration: 2);
            aircraft.Accumulator = Math.Min(aircraft.Accumulator + frameTime, FighterFlight.StepSeconds * 4);
            while (aircraft.Accumulator >= FighterFlight.StepSeconds)
            {
                aircraft.Accumulator -= FighterFlight.StepSeconds;
                var input = FighterInput.None;
                if (aircraft.FrontSeat is { } front && TryComp(front, out FighterSeatComponent? pilot))
                    input = pilot.CameraControl ? FighterInput.None : ActiveInput(pilot);
                var wasFlying = aircraft.Flying;
                var previousPhase = aircraft.Phase;
                FighterFlight.Step(aircraft, input, FighterFlight.StepSeconds);
                if (aircraft.Phase != previousPhase) PlayPhaseEffect(uid, aircraft.Phase);
                if (wasFlying && !aircraft.Flying && aircraft.Hull is { } hull)
                    _ambient.SetAmbience(hull, false);
                foreach (var seatUid in new[] { aircraft.FrontSeat, aircraft.RearSeat })
                    if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? sensor) && (!sensor.Pilot || sensor.CameraControl))
                        FighterFlight.StepSensor(aircraft, sensor, ActiveInput(sensor), FighterFlight.StepSeconds);
                if (aircraft.Mark != null)
                {
                    aircraft.MarkAge += FighterFlight.StepSeconds;
                    if (aircraft.MarkAge >= FighterFlight.MarkLifetime)
                        aircraft.Mark = null;
                }
            }
            // Move both camera transforms every simulation tick. Sending sensor
            // movement at the slower UI refresh rate made panning stop and jump
            // between updates, instead of using normal transform interpolation.
            foreach (var seatUid in new[] { aircraft.FrontSeat, aircraft.RearSeat })
                if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? component))
                {
                    UpdateCamera((seat, component), aircraft);
                    UpdateExteriorCamera(component, aircraft);
                }
            aircraft.NetworkAccumulator += frameTime;
            if (aircraft.NetworkAccumulator < .1f)
                continue;
            aircraft.NetworkAccumulator = 0;
            UpdateWorldEffects((uid, aircraft));
            if (TryComp(uid, out FighterWeaponsComponent? weapons) && _timing.CurTime >= weapons.NextRefresh)
                RefreshWeapons((uid, aircraft), weapons);
            foreach (var seatUid in new[] { aircraft.FrontSeat, aircraft.RearSeat })
            {
                if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? component))
                {
                    if (weapons != null)
                    {
                        UpdateLaser((seat, component), (uid, aircraft), weapons);
                        UpdateQueuedFire((seat, component), (uid, aircraft), weapons);
                    }
                    Dirty(seat, component);
                }
            }
            Dirty(uid, aircraft);
        }
        UpdateAirCombat();
        UpdateFighterSpectators();
    }

    private FighterInput ActiveInput(FighterSeatComponent seat) =>
        seat.Occupant is { } player && TryComp(player, out ActorComponent? _) &&
        _timing.CurTime - seat.LastInput < TimeSpan.FromSeconds(1) ? seat.Input : FighterInput.None;

    private void UpdateCamera(Entity<FighterSeatComponent> seat, FighterAircraftComponent aircraft)
    {
        if (seat.Comp.Camera is not { } camera || TerminatingOrDeleted(camera))
            return;
        if (FighterFlight.GroundScene(aircraft) && aircraft.GroundEntity is { } ground && Transform(ground).MapUid != null)
        {
            seat.Comp.SensorFocus ??= _transform.GetWorldPosition(ground);
            foreach (var groundCamera in new[] { seat.Comp.Camera, seat.Comp.ExteriorCamera })
            {
                if (groundCamera is not { } view || TerminatingOrDeleted(view)) continue;
                _transform.SetMapCoordinates(view, new MapCoordinates(seat.Comp.SensorLock ?? seat.Comp.SensorFocus.Value, Transform(ground).MapID));
                _transform.SetWorldRotation(view, Angle.Zero);
                _eye.SetZoom(view, new Vector2(1.4f));
                _eye.SetPvsScale(view, 3);
            }
            return;
        }
        // Each operator can investigate independently. Out-of-range locks keep their
        // coordinates, but stop subscribing to ground entities until they are in reach.
        var position = FighterFlight.SensorPosition(aircraft, seat.Comp);
        var available = FighterFlight.SensorAvailable(aircraft, position, seat.Comp);
        // Acquire once when terrain first enters reach, then leave the view anchored.
        if (available) seat.Comp.SensorFocus ??= position;
        _transform.SetMapCoordinates(camera, available
            ? new MapCoordinates(position, Comp<MapComponent>(aircraft.ViewMap).MapId)
            : _transform.GetMapCoordinates(seat.Owner));
        _transform.SetWorldRotation(camera, Angle.Zero);
        _eye.SetDrawLight(camera, FighterOptics.Mode(aircraft, _timing.CurTime) == FighterSensorMode.Normal);
        var zoomed = seat.Comp.Zoomed;
        var zoom = (1.2f + aircraft.Height / 1000f) * (zoomed ? .5f : 1f);
        _eye.SetZoom(camera, new Vector2(zoom));
        _eye.SetPvsScale(camera, zoom + .5f);
    }

    private void UpdateExteriorCamera(FighterSeatComponent seat, FighterAircraftComponent aircraft)
    {
        if (!FighterFlight.InAirspace(aircraft) ||
            seat.ExteriorCamera is not { } exterior || TerminatingOrDeleted(exterior))
            return;

        // Reconnaissance locks belong to the sensor, not the scenery. Stay on the
        // terrain map throughout the flight instead of jumping to the cockpit map
        // at each phase boundary; the cloud mask handles entering/leaving the AO.
        _transform.SetMapCoordinates(exterior, new MapCoordinates(aircraft.Position, Comp<MapComponent>(aircraft.ViewMap).MapId));
        // Replicate heading on the transform so the client interpolates turns
        // together with position. EyeComponent.Rotation is not networked.
        _transform.SetWorldRotation(exterior, new Angle(-aircraft.Heading));
        var zoom = 2f + aircraft.Height / 1000f;
        _eye.SetZoom(exterior, new Vector2(zoom));
        _eye.SetPvsScale(exterior, zoom * 1.7f);
    }
}
