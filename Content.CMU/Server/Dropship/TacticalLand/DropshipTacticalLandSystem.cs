using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server._RMC14.Dropship;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
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

public sealed partial class DropshipTacticalLandSystem : SharedDropshipTacticalLandSystem
{
    [Dependency] private SharedDropshipSystem _dropship = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private static readonly TimeSpan FootprintTickInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MaxTacticalHoverDuration = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan HoverReturnRetryInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextFootprintTick;

    private static readonly SoundSpecifier WarningSound =
        new SoundPathSpecifier("/Audio/_RMC14/Dropship/dropship_incoming.ogg");

    private const string EyePrototype = "CMUDropshipPilotEye";
    private const string WarningSignPrototype = "CMUHolographicWarningSign";
    private static readonly Vector2i ThirdPartyFootprint = new(7, 13);
    private const int LandingZoneExclusionRadius = 2;
    private const int TacticalHoverMapOffset = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipTacticalLandSessionComponent, BoundUIClosedEvent>(OnSessionUIClosed);
        SubscribeLocalEvent<DropshipTacticalLandSessionComponent, ComponentRemove>(OnSessionRemove);
        SubscribeLocalEvent<EphemeralDropshipDestinationComponent, DropshipRelayedEvent<FTLCompletedEvent>>(OnEphemeralFtlCompleted);
        SubscribeLocalEvent<DropshipTacticalHoverComponent, ComponentShutdown>(OnTacticalHoverShutdown);
    }

    protected override void OnTacticalLandStart(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandStartMsg args)
    {
        var pilot = args.Actor;

        if (HasComp<DropshipTacticalLandSessionComponent>(ent))
        {
            _popup.PopupEntity("This navigation console is already designating a tactical hover.", ent, pilot, PopupType.MediumCaution);
            return;
        }

        if (!CanDesignateTacticalLanding(ent))
        {
            _popup.PopupEntity("This navigation console cannot designate tactical landings.", ent, pilot, PopupType.MediumCaution);
            return;
        }

        if (Transform(ent).GridUid is not { } gridUid ||
            !TryComp(gridUid, out DropshipComponent? dropship) ||
            dropship.Crashed)
        {
            return;
        }

        if (HasComp<DropshipTacticalHoverComponent>(gridUid))
        {
            _popup.PopupEntity("This dropship is already in tactical hover.", ent, pilot, PopupType.MediumCaution);
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
        _zLevels.EnsureZLevelViewer(eye);


        var session = EnsureComp<DropshipTacticalLandSessionComponent>(ent);
        session.Pilot = pilot;
        session.Eye = eye;
        session.InitialMap = Transform(eye).MapUid;
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
        _popup.PopupEntity("Designating tactical landing site. Move to choose, adjust altitude for hover, then confirm.", ent, pilot, PopupType.Medium);
    }

    protected override void OnTacticalLandConfirm(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandConfirmMsg args)
    {
        if (!TryComp(ent, out DropshipTacticalLandSessionComponent? session) ||
            session.Pilot != args.Actor ||
            session.Eye is not { } eye)
        {
            return;
        }

        if (!CanDesignateTacticalLanding(ent))
        {
            _popup.PopupEntity("This navigation console cannot designate tactical landings.", ent, args.Actor, PopupType.MediumCaution);
            EndSession(ent, session);
            return;
        }

        if (!TryComp(eye, out TransformComponent? eyeXform))
        {
            EndSession(ent, session);
            return;
        }

        var tacticalHover = IsTacticalHover(session, eyeXform);

        if (!TryComp(eye, out DropshipPilotEyeComponent? pilotEye) || !pilotEye.ClearForLanding)
        {
            _popup.PopupEntity(tacticalHover
                ? "Hover site obstructed. Clear the highlighted tiles before committing."
                : "Landing site obstructed. Clear the highlighted tiles before committing.",
                ent,
                args.Actor,
                PopupType.MediumCaution);
            return;
        }

        EntityUid? returnDestination = null;
        if (tacticalHover)
        {
            if (Transform(ent).GridUid is not { } dropshipGrid ||
                !TryComp(dropshipGrid, out DropshipComponent? dropship) ||
                !TryGetReturnDestination(dropshipGrid, dropship, out var foundReturnDestination))
            {
                _popup.PopupEntity("Unable to determine a return destination for tactical hover.", ent, args.Actor, PopupType.MediumCaution);
                EndSession(ent, session);
                return;
            }

            returnDestination = foundReturnDestination;
        }

        var landingCoords = _transform.GetMapCoordinates(eye, eyeXform);
        var faction = GetConsoleFaction(ent) ?? GetPilotFaction(args.Actor) ?? string.Empty;

        var destination = Spawn(null, landingCoords);
        EnsureComp<DropshipDestinationComponent>(destination);
        _dropship.SetDestinationType(destination, DropshipDestinationComponent.DestinationType.Dropship.ToString());
        _dropship.SetFactionController(destination, faction);
        var ephemeral = EnsureComp<EphemeralDropshipDestinationComponent>(destination);
        ephemeral.TacticalHover = tacticalHover;
        if (tacticalHover)
        {
            ephemeral.ReturnDestination = returnDestination;
            ephemeral.MaxHoverTime = MaxTacticalHoverDuration;
            ephemeral.Footprint = pilotEye.Footprint;
        }

        if (!_dropship.FlyTo(ent, destination, args.Actor))
        {
            QueueDel(destination);
            return;
        }

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

        Entity<DropshipNavigationComputerComponent>? console = null;
        var consoleQuery = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var navUid, out var navComp, out var navXform))
        {
            if (navXform.GridUid == dropshipGrid)
            {
                console = (navUid, navComp);
                break;
            }
        }

        var footprint = console is { } c ? GetFootprint(c, dropship) : dropship.TacticalLandFootprint;

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

    protected override void OnTacticalLandMoveUp(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandMoveUpMsg args)
    {
        TryMoveTacticalEye(ent, args.Actor, TacticalHoverMapOffset);
    }

    protected override void OnTacticalLandMoveDown(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandMoveDownMsg args)
    {
        TryMoveTacticalEye(ent, args.Actor, -TacticalHoverMapOffset);
    }

    protected override void OnTacticalHoverCancel(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalHoverCancelMsg args)
    {
        if (Transform(ent).GridUid is not { } dropshipGrid ||
            !TryComp(dropshipGrid, out DropshipTacticalHoverComponent? hover))
        {
            return;
        }

        RequestTacticalHoverReturn((dropshipGrid, hover), args.Actor);
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
        if (ent.Comp.TacticalHover)
            StartTacticalHover(ent, args.Relayer);

        // Don't delete: dropship.Destination still references this entity; nav-console
        // RefreshUI calls Name(uid) on it. The ephemeral marker filters it from lists.
    }

    private void OnTacticalHoverShutdown(Entity<DropshipTacticalHoverComponent> ent, ref ComponentShutdown args)
    {
        CleanupHoverEffects(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        ProcessTacticalHovers(now);
        UpdateHoverEffects();

        if (now >= _nextFootprintTick)
        {
            _nextFootprintTick = now + FootprintTickInterval;

            var query = EntityQueryEnumerator<DropshipPilotEyeComponent, TransformComponent>();
            while (query.MoveNext(out var eyeUid, out var pilotEye, out var xform))
            {
                UpdateFootprint((eyeUid, pilotEye), xform);
            }
        }
    }

    private Vector2i GetFootprint(Entity<DropshipNavigationComputerComponent> console, DropshipComponent dropship)
    {
        if (console.Comp.TacticalLandFootprintOverride != Vector2i.Zero)
            return console.Comp.TacticalLandFootprintOverride;

        if (TryComp(console.Owner, out WhitelistedShuttleComponent? whitelist) &&
            string.Equals(whitelist.Faction, "thirdparty", StringComparison.OrdinalIgnoreCase))
        {
            return ThirdPartyFootprint;
        }
        return dropship.TacticalLandFootprint;
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

        if (!TryGetFootprintGrid(xform, out var gridUid, out var grid))
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
                    else
                    {
                        var opening = CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tile);
                        if (tileRef.Tile.IsEmpty && !opening)
                            blockedThis = true;
                        else if (!opening && _turf.IsTileBlocked(tileRef, blockMask))
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

        if (eye.Comp.Console is { } console &&
            eye.Comp.Pilot is { } pilot &&
            !TerminatingOrDeleted(console) &&
            TryComp(console, out DropshipNavigationComputerComponent? nav))
        {
            PushUiState((console, nav), pilot);
        }
    }

    private void TryMoveTacticalEye(Entity<DropshipNavigationComputerComponent> ent, EntityUid pilot, int offset)
    {
        if (!TryComp(ent, out DropshipTacticalLandSessionComponent? session) ||
            session.Pilot != pilot ||
            session.Eye is not { } eye)
        {
            return;
        }

        if (!TryComp(eye, out TransformComponent? xform) ||
            xform.MapUid is not { } map)
        {
            return;
        }

        if (!_zLevels.TryMapOffset(map, offset, out _, out var targetMap))
        {
            var direction = offset > 0 ? "higher" : "lower";
            _popup.PopupEntity($"No {direction} tactical hover level is available.", ent, pilot, PopupType.MediumCaution);
            PushUiState(ent, pilot);
            return;
        }

        var worldPosition = _transform.GetWorldPosition(eye);
        _transform.SetMapCoordinates(eye, new MapCoordinates(worldPosition, targetMap.MapId));
        _zLevels.EnsureZLevelViewer(eye);

        if (TryComp(eye, out DropshipPilotEyeComponent? pilotEye) &&
            TryComp(eye, out TransformComponent? movedXform))
        {
            UpdateFootprint((eye, pilotEye), movedXform);
        }

        PushUiState(ent, pilot);
        _popup.PopupEntity(offset > 0 ? "Tactical hover view moved up one level." : "Tactical hover view moved down one level.", ent, pilot);
    }

    private bool TryGetReturnDestination(EntityUid dropshipGrid, DropshipComponent dropship, out EntityUid destination)
    {
        if (dropship.Destination is { } current &&
            !TerminatingOrDeleted(current) &&
            !HasComp<EphemeralDropshipDestinationComponent>(current))
        {
            destination = current;
            return true;
        }

        if (dropship.DepartureLocation is { } departure &&
            !TerminatingOrDeleted(departure) &&
            !HasComp<EphemeralDropshipDestinationComponent>(departure))
        {
            destination = departure;
            return true;
        }

        destination = EntityUid.Invalid;
        EntityUid? closestDestination = null;
        var closestDistance = float.MaxValue;
        var dropshipMap = Transform(dropshipGrid).MapUid;
        var dropshipPosition = _transform.GetWorldPosition(dropshipGrid);

        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dest, out var xform))
        {
            if (HasComp<EphemeralDropshipDestinationComponent>(uid))
                continue;

            if (dest.Ship == dropshipGrid)
            {
                destination = uid;
                return true;
            }

            if (xform.MapUid != dropshipMap)
                continue;

            var distance = Vector2.DistanceSquared(dropshipPosition, _transform.GetWorldPosition(uid));
            if (distance >= closestDistance)
                continue;

            closestDestination = uid;
            closestDistance = distance;
        }

        if (closestDestination is not { } best)
            return false;

        destination = best;
        return true;
    }

    private void StartTacticalHover(Entity<EphemeralDropshipDestinationComponent> ent, EntityUid dropshipGrid)
    {
        if (ent.Comp.ReturnDestination is not { } returnDestination ||
            TerminatingOrDeleted(returnDestination))
        {
            Log.Warning($"Tactical hover destination {ToPrettyString(ent.Owner)} has no valid return destination.");
            return;
        }

        if (!TryComp(dropshipGrid, out DropshipComponent? dropship) || dropship.Crashed)
            return;

        var hover = EnsureComp<DropshipTacticalHoverComponent>(dropshipGrid);
        CleanupHoverEffects((dropshipGrid, hover));

        var duration = ent.Comp.MaxHoverTime;
        if (duration <= TimeSpan.Zero || duration > MaxTacticalHoverDuration)
            duration = MaxTacticalHoverDuration;

        hover.ReturnDestination = returnDestination;
        hover.HoverDestination = ent.Owner;
        hover.ReturnAt = _timing.CurTime + duration;
        hover.NextReturnAttempt = hover.ReturnAt;
        hover.Footprint = ent.Comp.Footprint.X > 0 && ent.Comp.Footprint.Y > 0
            ? ent.Comp.Footprint
            : dropship.TacticalLandFootprint;

        _zLevels.EnsureZLevelViewer(dropshipGrid);
        SpawnHoverShadow((dropshipGrid, hover));
        SpawnHoverDownwashes((dropshipGrid, hover));
    }

    private void ProcessTacticalHovers(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DropshipTacticalHoverComponent>();
        while (query.MoveNext(out var uid, out var hover))
        {
            if (now < hover.ReturnAt || now < hover.NextReturnAttempt)
                continue;

            hover.NextReturnAttempt = now + HoverReturnRetryInterval;

            if (!TryGetHoverReturnDestination((uid, hover), out var returnDestination))
                continue;

            TryReturnTacticalHover((uid, hover), returnDestination, null);
        }
    }

    private void RequestTacticalHoverReturn(Entity<DropshipTacticalHoverComponent> hover, EntityUid user)
    {
        if (!TryGetHoverReturnDestination(hover, out var returnDestination))
        {
            _popup.PopupEntity("Tactical hover return destination is no longer available.", hover.Owner, user, PopupType.MediumCaution);
            return;
        }

        var now = _timing.CurTime;
        hover.Comp.ReturnAt = now;
        hover.Comp.NextReturnAttempt = now;
        ShortenTacticalHoverCooldown(hover.Owner);

        if (TryReturnTacticalHover(hover, returnDestination, user))
            _popup.PopupEntity("Tactical hover returning.", hover.Owner, user, PopupType.Medium);
        else
            _popup.PopupEntity("Tactical hover return queued.", hover.Owner, user, PopupType.Medium);
    }

    private bool TryGetHoverReturnDestination(Entity<DropshipTacticalHoverComponent> hover, out EntityUid returnDestination)
    {
        if (hover.Comp.ReturnDestination is { } destination &&
            !TerminatingOrDeleted(destination))
        {
            returnDestination = destination;
            return true;
        }

        Log.Warning($"Tactical hover on {ToPrettyString(hover.Owner)} has no valid return destination.");
        RemCompDeferred<DropshipTacticalHoverComponent>(hover.Owner);
        returnDestination = EntityUid.Invalid;
        return false;
    }

    private bool TryReturnTacticalHover(Entity<DropshipTacticalHoverComponent> hover, EntityUid returnDestination, EntityUid? user)
    {
        if (!TryGetNavigationConsole(hover.Owner, out var console))
            return false;

        if (!_dropship.FlyTo(console, returnDestination, user))
            return false;

        if (hover.Comp.HoverDestination is { } hoverDestination &&
            !TerminatingOrDeleted(hoverDestination))
        {
            QueueDel(hoverDestination);
        }

        RemCompDeferred<DropshipTacticalHoverComponent>(hover.Owner);
        return true;
    }

    private void ShortenTacticalHoverCooldown(EntityUid dropshipGrid)
    {
        if (!TryComp(dropshipGrid, out FTLComponent? ftl) ||
            !TryComp(dropshipGrid, out DropshipComponent? dropship) ||
            ftl.State != FTLState.Cooldown)
        {
            return;
        }

        var returnAt = _timing.CurTime + dropship.CancelFlightTime;
        if (returnAt >= ftl.StateTime.End)
            return;

        ftl.StateTime.End = returnAt;
        Dirty(dropshipGrid, ftl);
    }

    private bool TryGetNavigationConsole(EntityUid dropshipGrid, out Entity<DropshipNavigationComputerComponent> console)
    {
        console = default;

        var query = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != dropshipGrid)
                continue;

            console = (uid, comp);
            return true;
        }

        return false;
    }

    private void UpdateHoverEffects()
    {
        var shadows = EntityQueryEnumerator<DropshipTacticalHoverShadowComponent>();
        while (shadows.MoveNext(out var uid, out var shadow))
        {
            UpdateHoverShadow((uid, shadow));
        }

        var downwashes = EntityQueryEnumerator<DropshipTacticalHoverDownwashComponent>();
        while (downwashes.MoveNext(out var uid, out var downwash))
        {
            UpdateHoverDownwash((uid, downwash));
        }
    }

    private void SpawnHoverShadow(Entity<DropshipTacticalHoverComponent> hover)
    {
        if (!TryGetHoverEffectCoordinates(hover.Owner, Vector2.Zero, hover.Comp.GroundMapOffset, out var coords, out var rotation))
            return;

        var shadow = Spawn(hover.Comp.ShadowPrototype, coords, rotation: rotation);
        var shadowComp = EnsureComp<DropshipTacticalHoverShadowComponent>(shadow);
        shadowComp.Dropship = hover.Owner;
        shadowComp.Footprint = hover.Comp.Footprint;
        shadowComp.ProjectedMapOffset = hover.Comp.GroundMapOffset;
        Dirty(shadow, shadowComp);

        hover.Comp.Shadow = shadow;
    }

    private void SpawnHoverDownwashes(Entity<DropshipTacticalHoverComponent> hover)
    {
        foreach (var offset in GetHoverDownwashOffsets(hover.Comp.Footprint))
        {
            if (!TryGetHoverEffectCoordinates(hover.Owner, offset, hover.Comp.GroundMapOffset, out var coords, out var rotation))
                continue;

            var downwash = Spawn(hover.Comp.DownwashPrototype, coords, rotation: rotation);
            var downwashComp = EnsureComp<DropshipTacticalHoverDownwashComponent>(downwash);
            downwashComp.Dropship = hover.Owner;
            downwashComp.Offset = offset;
            downwashComp.ProjectedMapOffset = hover.Comp.GroundMapOffset;
            Dirty(downwash, downwashComp);

            hover.Comp.Downwashes.Add(downwash);
        }
    }

    private void UpdateHoverShadow(Entity<DropshipTacticalHoverShadowComponent> shadow)
    {
        if (shadow.Comp.Dropship is not { } dropship ||
            TerminatingOrDeleted(dropship))
        {
            QueueDel(shadow);
            return;
        }

        if (!TryGetHoverEffectCoordinates(dropship, Vector2.Zero, shadow.Comp.ProjectedMapOffset, out var coords, out var rotation))
            return;

        _transform.SetMapCoordinates(shadow.Owner, coords);
        _transform.SetWorldRotation(shadow.Owner, rotation);
    }

    private void UpdateHoverDownwash(Entity<DropshipTacticalHoverDownwashComponent> downwash)
    {
        if (downwash.Comp.Dropship is not { } dropship ||
            TerminatingOrDeleted(dropship))
        {
            QueueDel(downwash);
            return;
        }

        if (!TryGetHoverEffectCoordinates(dropship, downwash.Comp.Offset, downwash.Comp.ProjectedMapOffset, out var coords, out var rotation))
            return;

        _transform.SetMapCoordinates(downwash.Owner, coords);
        _transform.SetWorldRotation(downwash.Owner, rotation);
    }

    private bool TryGetHoverEffectCoordinates(
        EntityUid dropship,
        Vector2 offset,
        int mapOffset,
        out MapCoordinates coords,
        out Angle rotation)
    {
        coords = default;
        rotation = _transform.GetWorldRotation(dropship);

        var xform = Transform(dropship);
        var worldPosition = _transform.GetWorldPosition(dropship) + rotation.RotateVec(offset);
        if (xform.MapUid is { } map &&
            TryProjectToGroundEffectMap(map, mapOffset, worldPosition, out coords))
        {
            return true;
        }

        if (xform.MapID == MapId.Nullspace)
            return false;

        coords = new MapCoordinates(worldPosition, xform.MapID);
        return true;
    }

    private bool TryProjectToGroundEffectMap(
        Entity<CMUZLevelMapComponent?> sourceMap,
        int startOffset,
        Vector2 worldPosition,
        out MapCoordinates coords)
    {
        coords = default;

        if (startOffset >= 0)
            startOffset = -1;

        MapComponent? lowestMap = null;

        for (var offset = startOffset;
             _zLevels.TryMapOffset(sourceMap, offset, out var projectedMap, out var projectedMapComp);
             offset--)
        {
            lowestMap = projectedMapComp;

            if (!HasSolidProjectionTile(projectedMap.Value.Owner, worldPosition))
                continue;

            coords = new MapCoordinates(worldPosition, projectedMapComp.MapId);
            return true;
        }

        if (lowestMap == null)
            return false;

        coords = new MapCoordinates(worldPosition, lowestMap.MapId);
        return true;
    }

    private bool HasSolidProjectionTile(EntityUid mapUid, Vector2 worldPosition)
    {
        if (!TryComp(mapUid, out MapGridComponent? grid) ||
            !_map.TryGetTileRef(mapUid, grid, worldPosition, out var tileRef))
        {
            return false;
        }

        return !CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tile);
    }

    private void CleanupHoverEffects(Entity<DropshipTacticalHoverComponent> hover)
    {
        if (hover.Comp.Shadow is { } shadow &&
            !TerminatingOrDeleted(shadow))
        {
            QueueDel(shadow);
        }

        hover.Comp.Shadow = null;

        foreach (var downwash in hover.Comp.Downwashes)
        {
            if (!TerminatingOrDeleted(downwash))
                QueueDel(downwash);
        }

        hover.Comp.Downwashes.Clear();
    }

    private bool TryGetFootprintGrid(TransformComponent xform, out EntityUid gridUid, out MapGridComponent grid)
    {
        if (xform.GridUid is { } xformGrid && TryComp(xformGrid, out MapGridComponent? xformGridComp))
        {
            gridUid = xformGrid;
            grid = xformGridComp;
            return true;
        }

        if (xform.MapUid is { } map && TryComp(map, out MapGridComponent? mapGridComp))
        {
            gridUid = map;
            grid = mapGridComp;
            return true;
        }

        gridUid = EntityUid.Invalid;
        grid = default!;
        return false;
    }

    private static IEnumerable<Vector2> GetHoverDownwashOffsets(Vector2i footprint)
    {
        var x = MathF.Max(1f, footprint.X / 2f - 1.5f);
        var y = MathF.Max(1f, footprint.Y / 2f - 1.5f);

        yield return new Vector2(-x, -y);
        yield return new Vector2(x, -y);
        yield return new Vector2(-x, y);
        yield return new Vector2(x, y);
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
        var clearForLanding = TryComp(eye, out DropshipPilotEyeComponent? pilotEye) && pilotEye.ClearForLanding;
        var tacticalHover = TryComp(eye, out TransformComponent? eyeXform) && IsTacticalHover(session, eyeXform);
        var canMoveUp = CanMoveTacticalEye(eye, TacticalHoverMapOffset);
        var canMoveDown = CanMoveTacticalEye(eye, -TacticalHoverMapOffset);
        var state = new DropshipNavigationTacticalLandBuiState(GetNetEntity(eye), clearForLanding, tacticalHover, canMoveUp, canMoveDown, doorLocks, false);
        _ui.SetUiState(ent.Owner, DropshipNavigationUiKey.Key, state);
    }

    public bool TryRefreshActiveSessionUi(Entity<DropshipNavigationComputerComponent> ent)
    {
        if (!TryComp(ent, out DropshipTacticalLandSessionComponent? session) ||
            session.Pilot is not { } pilot ||
            session.Eye is null)
        {
            return false;
        }

        PushUiState(ent, pilot);
        return true;
    }

    private bool CanMoveTacticalEye(EntityUid eye, int offset)
    {
        return TryComp(eye, out TransformComponent? xform) &&
               xform.MapUid is { } map &&
               _zLevels.TryMapOffset(map, offset, out _);
    }

    private static bool IsTacticalHover(DropshipTacticalLandSessionComponent session, TransformComponent eyeXform)
    {
        return session.InitialMap is { } initialMap &&
               eyeXform.MapUid is { } currentMap &&
               currentMap != initialMap;
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

    private bool CanDesignateTacticalLanding(Entity<DropshipNavigationComputerComponent> console)
    {
        if (console.Comp.CanTacticalLand)
            return true;

        return TryComp(console.Owner, out WhitelistedShuttleComponent? whitelist) &&
               string.Equals(whitelist.Faction, "thirdparty", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetPilotFaction(EntityUid pilot)
    {
        if (TryComp(pilot, out MarineComponent? marine) && !string.IsNullOrEmpty(marine.Faction))
            return marine.Faction;

        return null;
    }
}
