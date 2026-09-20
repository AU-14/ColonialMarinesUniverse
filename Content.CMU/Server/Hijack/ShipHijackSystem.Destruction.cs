using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Coordinates;
using Content.Shared.Destructible;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Hijack;

public sealed partial class ShipHijackSystem
{
    [Dependency] private RMCCameraShakeSystem _shake = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GameTicker _ticker = default!;

    private void InitializeDestruction()
    {
        SubscribeLocalEvent<CMUShipRoundEndAttemptEvent>(OnRoundEndAttempt);
        SubscribeLocalEvent<ShuttleFTLSafetyEvent>(OnFlightAttempt);
    }

    private void OnRoundEndAttempt(ref CMUShipRoundEndAttemptEvent args)
    {
        args.Cancelled |= HasDetonationInProgress();
    }

    private void OnFlightAttempt(ref ShuttleFTLSafetyEvent args)
    {
        if (TryGetShip(args.Shuttle, out var ship) &&
            (ship.Comp.InFTL || ship.Comp.Stage == CMUShipHijackStage.Destroyed ||
             ship.Comp.Stage == CMUShipHijackStage.GroundCrash &&
                 (!ship.Comp.GroundImpacted || HasComp<DropshipComponent>(args.Shuttle))) ||
            TryComp(args.Shuttle, out FTLComponent? ftl) && ftl.TargetCoordinates.EntityId != args.Shuttle &&
            !CanArrive(ftl.TargetCoordinates.EntityId))
        {
            args.Cancelled = true;
            args.Reason = Loc.GetString("cmu-hijack-launch-unavailable");
        }
    }

    private void SetFlightVisuals(Entity<CMUShipHijackComponent> ship, bool ftl)
    {
        foreach (var map in ship.Comp.ShipMaps)
        {
            if (TerminatingOrDeleted(map))
                continue;
            var parallax = EnsureComp<ParallaxComponent>(map);
            if (ftl)
            {
                ship.Comp.OriginalParallax.TryAdd(map, parallax.Parallax);
                parallax.Parallax = "CMUHijackFTL";
            }
            else if (ship.Comp.OriginalParallax.Remove(map, out var original))
                parallax.Parallax = original;
            Dirty(map, parallax);
        }
        _shake.ShakeCamera(ShipFilter(ship), ftl ? 10 : 30, ftl ? 2 : 5);
    }

    private void WarnPumps(Entity<CMUShipHijackComponent> ship)
    {
        Announce(ship, "cmu-hijack-pumps-overloading");
        foreach (var pump in ship.Comp.Pumps.Values)
        {
            if (pump is not { } uid || TerminatingOrDeleted(uid))
                continue;
            Spawn("CMUHijackPumpExplosionWarning", uid.ToCoordinates());
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Ambience/Objects/gas_hiss.ogg"), uid);
        }
    }

    private void ExplodePumps(Entity<CMUShipHijackComponent> ship)
    {
        foreach (var pump in ship.Comp.Pumps.Values)
        {
            if (pump is not { } uid || TerminatingOrDeleted(uid))
                continue;
            // swing_rockets.hit_target: twelve strikes within seven tiles, 0.1s apart.
            var center = _transform.GetMapCoordinates(uid);
            for (var i = 0; i < 12; i++)
                ship.Comp.PumpBlastTargets.Add(center.Offset(new Vector2(_random.Next(-7, 8), _random.Next(-7, 8))));
        }
        if (ship.Comp.PumpBlastTargets.Count > 0)
            ship.Comp.NextPumpBlastAt ??= _timing.CurTime;
    }

    private void ProcessPumpBlasts(Entity<CMUShipHijackComponent> ship)
    {
        if (ship.Comp.NextPumpBlastAt is not { } next || _timing.CurTime < next)
            return;
        var target = ship.Comp.PumpBlastTargets[0];
        ship.Comp.PumpBlastTargets.RemoveAt(0);
        if (_maps.MapExists(target.MapId))
            QueueCellExplosion(target, ship, 100, 10);
        ship.Comp.NextPumpBlastAt = ship.Comp.PumpBlastTargets.Count > 0
            ? _timing.CurTime + TimeSpan.FromSeconds(0.1) : null;
    }

    private void ExplodeApcs(Entity<CMUShipHijackComponent> ship)
    {
        var query = EntityQueryEnumerator<RMCApcComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid is not { } map || !ship.Comp.ShipMaps.Contains(map) || !_random.Prob(0.5f))
                continue;
            QueueCellExplosion(_transform.GetMapCoordinates(uid), uid, 30, 5);
        }
    }

    private void ShortWreckApcs(Entity<CMUShipHijackComponent> ship)
    {
        var query = EntityQueryEnumerator<RMCApcComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var apc, out var transform))
        {
            if (apc.Broken || transform.MapUid is not { } map ||
                !ship.Comp.ShipMaps.Contains(map) || !_random.Prob(0.05f))
                continue;
            // CMU's repairable electrical fault disables the APC until its wires are repaired.
            var broken = new BreakageEventArgs();
            RaiseLocalEvent(uid, broken);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks4.ogg"), uid);
        }
    }

    private void HeatEngineRoom(Entity<CMUShipHijackComponent> ship, float kelvin)
    {
        var engineAreas = new HashSet<EntityUid>();
        var reactors = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (reactors.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid is { } map && ship.Comp.ShipMaps.Contains(map) &&
                _areas.TryGetArea(uid.ToCoordinates(), out var area, out _))
                engineAreas.Add(area.Value);
        }
        foreach (var area in engineAreas)
            EnsureComp<CMUOverheatedAreaComponent>(area).Temperature = kelvin;
        foreach (var gridUid in ship.Comp.ShipGrids)
        {
            if (!TryComp(gridUid, out MapGridComponent? grid) || !TryComp(gridUid, out AreaGridComponent? areas))
                continue;
            foreach (var tile in _maps.GetAllTiles(gridUid, grid))
            {
                if (!_areas.TryGetArea((gridUid, grid, areas), tile.GridIndices, out var area, out _) ||
                    !engineAreas.Contains(area.Value))
                    continue;
                var air = _atmosphere.GetTileMixture(gridUid, Transform(gridUid).MapUid, tile.GridIndices, true);
                if (air != null && !air.Immutable)
                    air.Temperature = kelvin;
            }
        }
    }

    private void BeginDetonation(Entity<CMUShipHijackComponent> ship)
    {
        ship.Comp.Stage = CMUShipHijackStage.Detonating;
        ship.Comp.DetonationStartedAt = _timing.CurTime;
        ship.Comp.DetonationStep = 0;
        Announce(ship, "cmu-hijack-deck-creak");
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/CMU14/Private/Ambience/ship/ship_move_creaky_noise.ogg"), ShipFilter(ship), true);
        Dirty(ship);
    }

    public bool IsInDestructionZone(Entity<CMUShipHijackComponent> ship, EntityUid uid)
    {
        var transform = Transform(uid);
        return transform.MapUid is { } map &&
               (ship.Comp.ShipMaps.Contains(map) || ship.Comp.GroundImpacted && ship.Comp.GroundMaps.Contains(map));
    }

    private Filter CinematicFilter(Entity<CMUShipHijackComponent> ship)
    {
        var filter = Filter.Empty();
        foreach (var session in Filter.Broadcast().Recipients)
        {
            if (session.AttachedEntity is { } uid &&
                (ship.Comp.CinematicMobs.Contains(uid) || HasComp<GhostComponent>(uid)))
                filter.AddPlayer(session);
        }
        return filter;
    }

    private void ProcessDetonation(Entity<CMUShipHijackComponent> ship)
    {
        if (ship.Comp.DetonationStartedAt is not { } started)
            return;
        var elapsed = (_timing.CurTime - started).TotalSeconds;
        if (ship.Comp.DetonationStep == 0 && elapsed >= 7)
        {
            ship.Comp.DetonationStep++;
            _shake.ShakeCamera(ShipFilter(ship), 100, 2);
            Announce(ship, "cmu-hijack-meltdown");
        }
        if (ship.Comp.DetonationStep == 1 && elapsed >= 12)
        {
            ship.Comp.DetonationStep++;
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/CMU14/RoundEnd/nuclear_detonation1.ogg"), Filter.Broadcast(), true);
            var mobs = EntityQueryEnumerator<MobStateComponent>();
            while (mobs.MoveNext(out var uid, out var mob))
            {
                if (mob.CurrentState == MobState.Dead || IsInDestructionZone(ship, uid))
                    ship.Comp.CinematicMobs.Add(uid);
            }
            _shake.ShakeCamera(CinematicFilter(ship), 110, 4);
        }
        if (ship.Comp.DetonationStep == 2 && elapsed >= 22)
        {
            ship.Comp.DetonationStep++;
            RaiseNetworkEvent(new CMUShipCinematicEvent(CMUShipCinematicStage.Ship), CinematicFilter(ship));
        }
        if (ship.Comp.DetonationStep == 3 && elapsed >= 23.5)
        {
            ship.Comp.DetonationStep++;
            RaiseNetworkEvent(new CMUShipCinematicEvent(CMUShipCinematicStage.Detonation), CinematicFilter(ship));
        }
        if (ship.Comp.DetonationStep == 4 && elapsed >= 27)
        {
            ship.Comp.DetonationStep++;
            // Recheck positions at detonation: entities on departed boats/pods are safe.
            foreach (var uid in ship.Comp.CinematicMobs)
            {
                if (TerminatingOrDeleted(uid) || !TryComp(uid, out MobStateComponent? mob) ||
                    mob.CurrentState == MobState.Dead || !IsInDestructionZone(ship, uid))
                    continue;
                if (_containers.TryGetContainingContainer(uid, out var shelter) &&
                    HasComp<CMUNuclearShelterComponent>(shelter.Owner))
                    continue;
                _mobState.ChangeMobState(uid, MobState.Dead, mob);
            }
            foreach (var session in CinematicFilter(ship).Recipients.ToArray())
            {
                var show = session.AttachedEntity is { } uid &&
                    (HasComp<GhostComponent>(uid) || _mobState.IsDead(uid) || IsInDestructionZone(ship, uid));
                RaiseNetworkEvent(new CMUShipCinematicEvent(show ? CMUShipCinematicStage.Destroyed : CMUShipCinematicStage.Clear), session);
            }
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/explosionfar.ogg"), Filter.Broadcast(), true);
        }
        if (ship.Comp.DetonationStep == 5 && elapsed >= 27.5)
        {
            ship.Comp.DetonationStep++;
            ship.Comp.Stage = CMUShipHijackStage.Destroyed;
            if (TryComp(ship, out EvacuationProgressComponent? progress))
            {
                progress.SelfDestructed = true;
                Dirty(ship.Owner, progress);
            }
            Dirty(ship);
            if (!_distress.TryEndActiveDistressRound(DistressSignalRuleResult.AllDied, "cmu-hijack-self-destruct-result"))
                _ticker.EndRound(Loc.GetString("cmu-hijack-self-destruct-result"));
        }
    }
}
