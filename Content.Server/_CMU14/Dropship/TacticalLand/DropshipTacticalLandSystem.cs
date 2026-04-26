using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Dropship;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Round;
using Content.Shared.Coordinates;
using Content.Shared.Doors.Components;
using Content.Shared.Eye;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Dropship.TacticalLand;

public sealed class DropshipTacticalLandSystem : SharedDropshipTacticalLandSystem
{
    [Dependency] private readonly SharedDropshipSystem _dropship = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AreaSystem _area = default!;

    private static readonly TimeSpan FootprintTickInterval = TimeSpan.FromMilliseconds(150);
    private TimeSpan _nextFootprintTick;

    private static readonly SoundSpecifier WarningSound =
        new SoundPathSpecifier("/Audio/_RMC14/Dropship/dropship_incoming.ogg");

    private const string EyePrototype = "CMUDropshipPilotEye";
    private const string WarningSignPrototype = "CMUHolographicWarningSign";
    private static readonly Vector2i ThirdPartyFootprint = new(7, 13);
    private const int LandingZoneExclusionRadius = 2;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipTacticalLandSessionComponent, BoundUIClosedEvent>(OnSessionUIClosed);
        SubscribeLocalEvent<DropshipTacticalLandSessionComponent, ComponentRemove>(OnSessionRemove);
        SubscribeLocalEvent<EphemeralDropshipDestinationComponent, DropshipRelayedEvent<FTLCompletedEvent>>(OnEphemeralFtlCompleted);
    }

    protected override void OnTacticalLandStart(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandStartMsg args)
    {
        var pilot = args.Actor;

        if (HasComp<DropshipTacticalLandSessionComponent>(ent))
            return;

        if (Transform(ent).GridUid is not { } gridUid ||
            !TryComp(gridUid, out DropshipComponent? dropship) ||
            dropship.Crashed)
        {
            return;
        }

        if (TryComp(gridUid, out FTLComponent? ftl) && ftl.State != FTLState.Cooldown && ftl.State != FTLState.Available)
        {
            _popup.PopupEntity("Cannot designate a tactical landing while the dropship is in flight.", ent, pilot, PopupType.MediumCaution);
            return;
        }

        var spawnCoords = FindInitialEyeCoordinates(ent, pilot, gridUid, dropship);
        if (spawnCoords is null)
        {
            _popup.PopupEntity("No suitable ground map detected for a tactical landing.", ent, pilot, PopupType.MediumCaution);
            return;
        }

        var eye = Spawn(EyePrototype, spawnCoords.Value);
        var eyeComp = EnsureComp<DropshipPilotEyeComponent>(eye);
        eyeComp.Pilot = pilot;
        eyeComp.Console = ent;
        eyeComp.Footprint = GetFootprint(ent, dropship);
        eyeComp.BlockedTiles.Clear();
        eyeComp.ClearForLanding = false;
        Dirty(eye, eyeComp);


        var session = EnsureComp<DropshipTacticalLandSessionComponent>(ent);
        session.Pilot = pilot;
        session.Eye = eye;
        Dirty(ent, session);

        if (TryComp(pilot, out EyeComponent? pilotEye))
        {
            session.OriginalZoom = pilotEye.Zoom;
            session.OriginalPvsScale = pilotEye.PvsScale;
            Dirty(ent, session);

            _eye.SetTarget(pilot, eye, pilotEye);
            _eye.SetDrawFov(pilot, true, pilotEye);
            _eye.SetPvsScale((pilot, pilotEye), 2.25f);
        }

        _mover.SetRelay(pilot, eye);

        PushUiState(ent, pilot);
        _popup.PopupEntity("Designating tactical landing site. Move to choose. Confirm to commit.", ent, pilot, PopupType.Medium);
    }

    protected override void OnTacticalLandConfirm(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandConfirmMsg args)
    {
        if (!TryComp(ent, out DropshipTacticalLandSessionComponent? session) ||
            session.Pilot != args.Actor ||
            session.Eye is not { } eye)
        {
            return;
        }

        if (!TryComp(eye, out TransformComponent? eyeXform))
        {
            EndSession(ent, session);
            return;
        }

        if (!TryComp(eye, out DropshipPilotEyeComponent? pilotEye) || !pilotEye.ClearForLanding)
        {
            _popup.PopupEntity("Drop site obstructed. Clear the highlighted tiles before landing.", ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        var landingCoords = eyeXform.Coordinates;
        var faction = GetConsoleFaction(ent) ?? GetPilotFaction(args.Actor) ?? string.Empty;

        var destination = Spawn(null, landingCoords);
        EnsureComp<DropshipDestinationComponent>(destination);
        _dropship.SetDestinationType(destination, DropshipDestinationComponent.DestinationType.Dropship.ToString());
        _dropship.SetFactionController(destination, faction);
        EnsureComp<EphemeralDropshipDestinationComponent>(destination);

        _dropship.FlyTo(ent, destination, args.Actor);

        EndSession(ent, session);
    }

    public void SpawnLandingWarning(Entity<DropshipDestinationComponent> destination, EntityUid dropshipGrid, FTLComponent ftl)
    {
        if (HasComp<DropshipLandingMarkersSpawnedComponent>(destination))
            return;

        if (!TryComp(destination.Owner, out TransformComponent? xform))
            return;

        if (!TryComp(dropshipGrid, out DropshipComponent? dropship))
            return;

        var destCoords = xform.Coordinates;
        var remaining = ftl.StateTime.End - _timing.CurTime;
        var lifetime = (float)remaining.TotalSeconds + 1f;
        if (lifetime < 2f)
            lifetime = 2f;

        EntityUid? consoleUid = null;
        var consoleQuery = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var navUid, out _, out var navXform))
        {
            if (navXform.GridUid == dropshipGrid)
            {
                consoleUid = navUid;
                break;
            }
        }

        var footprint = consoleUid is { } c ? GetFootprint(c, dropship) : dropship.TacticalLandFootprint;

        _audio.PlayPvs(WarningSound, destCoords, AudioParams.Default.WithVolume(2f));
        SpawnWarningBorder(destCoords, footprint, lifetime);

        EnsureComp<DropshipLandingMarkersSpawnedComponent>(destination);
    }

    public void ClearLandingWarning(EntityUid destination)
    {
        RemCompDeferred<DropshipLandingMarkersSpawnedComponent>(destination);
    }

    protected override void OnTacticalLandCancel(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandCancelMsg args)
    {
        if (!TryComp(ent, out DropshipTacticalLandSessionComponent? session))
            return;

        if (session.Pilot != args.Actor)
            return;

        EndSession(ent, session);
    }

    private void OnSessionUIClosed(Entity<DropshipTacticalLandSessionComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not DropshipNavigationUiKey)
            return;

        if (ent.Comp.Pilot != args.Actor)
            return;

        EndSession(ent.Owner, ent.Comp);
    }

    private void OnSessionRemove(Entity<DropshipTacticalLandSessionComponent> ent, ref ComponentRemove args)
    {
        TeardownEye(ent.Comp);
    }

    private void OnEphemeralFtlCompleted(Entity<EphemeralDropshipDestinationComponent> ent, ref DropshipRelayedEvent<FTLCompletedEvent> args)
    {
        // Don't delete: dropship.Destination still references this entity; nav-console
        // RefreshUI calls Name(uid) on it. The ephemeral marker filters it from lists.
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now < _nextFootprintTick)
            return;

        _nextFootprintTick = now + FootprintTickInterval;

        var query = EntityQueryEnumerator<DropshipPilotEyeComponent, TransformComponent>();
        while (query.MoveNext(out var eyeUid, out var pilotEye, out var xform))
        {
            UpdateFootprint((eyeUid, pilotEye), xform);
        }
    }

    private Vector2i GetFootprint(EntityUid console, DropshipComponent dropship)
    {
        if (TryComp(console, out WhitelistedShuttleComponent? whitelist) &&
            string.Equals(whitelist.Faction, "thirdparty", StringComparison.OrdinalIgnoreCase))
        {
            return ThirdPartyFootprint;
        }
        return dropship.TacticalLandFootprint;
    }

    private bool IsCeilingLevelZero(EntityCoordinates coords)
    {
        if (!_area.CanOrbitalBombard(coords, out _)) return false;
        if (!_area.CanCAS(coords)) return false;
        if (!_area.CanSupplyDrop(_transform.ToMapCoordinates(coords))) return false;
        if (!_area.CanMortarFire(coords)) return false;
        if (!_area.CanMortarPlacement(coords)) return false;
        if (!_area.CanLase(coords)) return false;
        if (!_area.CanMedevac(coords)) return false;
        if (!_area.CanParadrop(coords)) return false;
        return true;
    }

    private void SpawnWarningBorder(EntityCoordinates center, Vector2i footprint, float lifetime)
    {
        var halfW = footprint.X / 2;
        var halfH = footprint.Y / 2;

        for (var dx = -halfW; dx <= halfW; dx += 2)
        {
            SpawnTimed(center.Offset(new Vector2(dx,  halfH)), lifetime);
            SpawnTimed(center.Offset(new Vector2(dx, -halfH)), lifetime);
        }

        for (var dy = -halfH + 2; dy <= halfH - 2; dy += 2)
        {
            SpawnTimed(center.Offset(new Vector2( halfW, dy)), lifetime);
            SpawnTimed(center.Offset(new Vector2(-halfW, dy)), lifetime);
        }
    }

    private void SpawnTimed(EntityCoordinates coords, float lifetime)
    {
        var ent = Spawn(WarningSignPrototype, coords);
        var despawn = EnsureComp<TimedDespawnComponent>(ent);
        despawn.Lifetime = lifetime;
    }

    private void UpdateFootprint(Entity<DropshipPilotEyeComponent> eye, TransformComponent xform)
    {
        var w = eye.Comp.Footprint.X;
        var h = eye.Comp.Footprint.Y;
        var halfW = w / 2;
        var halfH = h / 2;

        var blocked = new List<Vector2i>();
        var allBlocked = false;

        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
        {
            allBlocked = true;
        }
        else
        {
            var centerTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            const CollisionGroup blockMask = CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable;

            var destinationTiles = new HashSet<Vector2i>();
            var destQuery = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
            while (destQuery.MoveNext(out var destUid, out _, out var destXform))
            {
                if (HasComp<EphemeralDropshipDestinationComponent>(destUid))
                    continue;
                if (destXform.GridUid != gridUid)
                    continue;

                var destTile = _map.CoordinatesToTile(gridUid, grid, destXform.Coordinates);
                for (var ldx = -LandingZoneExclusionRadius; ldx <= LandingZoneExclusionRadius; ldx++)
                {
                    for (var ldy = -LandingZoneExclusionRadius; ldy <= LandingZoneExclusionRadius; ldy++)
                    {
                        var ddx = destTile.X + ldx - centerTile.X;
                        var ddy = destTile.Y + ldy - centerTile.Y;
                        if (Math.Abs(ddx) <= halfW && Math.Abs(ddy) <= halfH)
                            destinationTiles.Add(new Vector2i(ddx, ddy));
                    }
                }
            }


            for (var dx = -halfW; dx <= halfW; dx++)
            {
                for (var dy = -halfH; dy <= halfH; dy++)
                {
                    var t = new Vector2i(centerTile.X + dx, centerTile.Y + dy);
                    var blockedThis = false;

                    if (destinationTiles.Contains(new Vector2i(dx, dy)))
                    {
                        blockedThis = true;
                    }
                    else if (!_map.TryGetTileRef(gridUid, grid, t, out var tileRef))
                    {
                        blockedThis = true;
                    }
                    else if (tileRef.Tile.IsEmpty)
                    {
                        blockedThis = true;
                    }
                    else if (_turf.IsTileBlocked(tileRef, blockMask))
                    {
                        blockedThis = true;
                    }
                    else if (!IsCeilingLevelZero(new EntityCoordinates(gridUid, new Vector2(t.X + 0.5f, t.Y + 0.5f))))
                    {
                        blockedThis = true;
                    }

                    if (blockedThis)
                        blocked.Add(new Vector2i(dx, dy));
                }
            }
        }

        if (allBlocked)
        {
            blocked.Clear();
            for (var dx = -halfW; dx <= halfW; dx++)
            for (var dy = -halfH; dy <= halfH; dy++)
                blocked.Add(new Vector2i(dx, dy));
        }

        var clear = blocked.Count == 0;
        if (eye.Comp.ClearForLanding == clear &&
            eye.Comp.BlockedTiles.Count == blocked.Count &&
            eye.Comp.BlockedTiles.SequenceEqual(blocked))
        {
            return;
        }

        eye.Comp.ClearForLanding = clear;
        eye.Comp.BlockedTiles = blocked;
        Dirty(eye, eye.Comp);
    }

    private void EndSession(EntityUid console, DropshipTacticalLandSessionComponent session)
    {
        TeardownEye(session);
        RemCompDeferred<DropshipTacticalLandSessionComponent>(console);
    }

    private void TeardownEye(DropshipTacticalLandSessionComponent session)
    {
        if (session.Pilot is { } pilot && !TerminatingOrDeleted(pilot))
        {
            if (TryComp(pilot, out EyeComponent? pilotEye))
            {
                _eye.SetTarget(pilot, null, pilotEye);
                _eye.SetDrawFov(pilot, true, pilotEye);
                _eye.SetPvsScale((pilot, pilotEye), session.OriginalPvsScale);
            }

            RemComp<RelayInputMoverComponent>(pilot);
        }

        if (session.Eye is { } eye && !TerminatingOrDeleted(eye))
            QueueDel(eye);

        session.Eye = null;
        session.Pilot = null;
    }

    private void PushUiState(Entity<DropshipNavigationComputerComponent> ent, EntityUid pilot)
    {
        if (!TryComp(ent, out DropshipTacticalLandSessionComponent? session) || session.Eye is not { } eye)
            return;

        var doorLocks = new Dictionary<DoorLocation, bool>();
        var state = new DropshipNavigationTacticalLandBuiState(GetNetEntity(eye), true, doorLocks, false);
        _ui.SetUiState(ent.Owner, DropshipNavigationUiKey.Key, state);
    }

    private EntityCoordinates? FindInitialEyeCoordinates(EntityUid console, EntityUid pilot, EntityUid dropshipGrid, DropshipComponent dropship)
    {
        var faction = GetConsoleFaction(console) ?? GetPilotFaction(pilot);

        EntityUid? bestFlare = null;
        var bestTime = TimeSpan.MinValue;
        var flareQuery = EntityQueryEnumerator<DropshipTargetComponent, FlareSignalComponent, TransformComponent>();
        while (flareQuery.MoveNext(out var uid, out var target, out _, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<RMCPlanetComponent>(map))
                continue;

            var creatorFaction = target.CreatorFaction;
            if (!string.IsNullOrEmpty(creatorFaction) &&
                !string.IsNullOrEmpty(faction) &&
                !string.Equals(creatorFaction, faction, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var activatedAt = target.ActivatedAt;
            if (activatedAt > bestTime)
            {
                bestTime = activatedAt;
                bestFlare = uid;
            }
        }

        if (bestFlare != null)
            return Transform(bestFlare.Value).Coordinates;

        if (dropship.LastLandingCoordinates is { } netCoords)
            return GetCoordinates(netCoords);

        return GetPlanetCenter();
    }

    private EntityCoordinates? GetPlanetCenter()
    {
        EntityUid? planetMap = null;
        var planetQuery = EntityQueryEnumerator<RMCPlanetComponent>();
        while (planetQuery.MoveNext(out var mapUid, out _))
        {
            planetMap = mapUid;
            break;
        }

        if (planetMap is null)
            return null;

        EntityUid? bestGrid = null;
        var bestBounds = default(Box2);
        var bestArea = 0f;
        var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var grid, out var gridXform))
        {
            if (gridXform.MapUid != planetMap)
                continue;

            var bounds = grid.LocalAABB;
            var area = bounds.Width * bounds.Height;
            if (area <= bestArea)
                continue;

            bestGrid = gridUid;
            bestBounds = bounds;
            bestArea = area;
        }

        if (bestGrid is { } g)
            return new EntityCoordinates(g, bestBounds.Center);

        return new EntityCoordinates(planetMap.Value, Vector2.Zero);
    }

    private string? GetConsoleFaction(EntityUid console)
    {
        if (TryComp(console, out WhitelistedShuttleComponent? whitelist) && !string.IsNullOrEmpty(whitelist.Faction))
            return whitelist.Faction;

        return null;
    }

    private string? GetPilotFaction(EntityUid pilot)
    {
        if (TryComp(pilot, out MarineComponent? marine) && !string.IsNullOrEmpty(marine.Faction))
            return marine.Faction;

        return null;
    }
}
