using System.Linq;
using Content.Server._RMC14.Rules;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared.Audio;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Coordinates;
using Content.Shared.Maps;
using Content.Shared.Power;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Hijack;

/// <summary>
/// CM-SS13 SShijack's ship flight and reactor state machine. Delays live on the map
/// component, so deletion, round restart and map pause cannot leave orphan timers.
/// </summary>
public sealed partial class ShipHijackSystem : CMUShipHijackSystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedRMCPowerSystem _power = default!;
    [Dependency] private SharedMarineAnnounceSystem _announcements = default!;
    [Dependency] private SharedXenoAnnounceSystem _xenoAnnouncements = default!;
    [Dependency] private CMUZLevelsSystem _levels = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedRMCExplosionSystem _explosions = default!;
    [Dependency] private SharedRMCFlammableSystem _fire = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMDistressSignalRuleSystem _distress = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnHijack, after: [typeof(SharedEvacuationSystem)]);
        SubscribeLocalEvent<CMUShipHullComponent, MapInitEvent>(OnHullMapInit);
        InitializeReactors();
        InitializeDestruction();
    }

    private void OnHullMapInit(Entity<CMUShipHullComponent> ent, ref MapInitEvent args)
    {
        // Empty background grids let Z physics continue through holes after bodies
        // leave a deck. The actual floor remains on the movable hull grid.
        if (HasComp<CMUZLevelDeckComponent>(ent) && Transform(ent).MapUid is { } map)
            EnsureComp<MapGridComponent>(map);

        // Non-map grids receive an enabled Shuttle on startup. Freeze the mainship
        // after that initialization; ground impact moves its transform explicitly.
        if (TryComp<ShuttleComponent>(ent, out var shuttle))
        {
            shuttle.Enabled = false;
            _shuttle.Disable(ent);
        }
    }

    private void OnHijack(ref DropshipHijackLandedEvent args)
    {
        if (!TryGetShip(args.Map, out var ship) || ship.Comp.Stage != CMUShipHijackStage.Idle)
            return;

        ship.Comp.ShipMaps = _levels.GetAllNetworkMaps(ship).ToHashSet();
        ship.Comp.VictimFaction = args.VictimFaction;
        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var grid, out var transform))
        {
            if (transform.MapUid is { } map && ship.Comp.ShipMaps.Contains(map) && HasComp<AreaGridComponent>(uid))
            {
                // The wreck remains one deck with breaches, as in BYOND. Engine grid
                // splitting would discard area ownership from detached sections.
                grid.CanSplit = false;
                ship.Comp.ShipGrids.Add(uid);
            }
        }

        var pumps = EntityQueryEnumerator<EvacuationPumpComponent, TransformComponent>();
        while (pumps.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid is not { } map || !ship.Comp.ShipMaps.Contains(map))
                continue;
            EnsureComp<CMUHijackPumpComponent>(uid);
            if (_areas.TryGetArea(uid.ToCoordinates(), out var area, out _))
                ship.Comp.Pumps[area.Value] = uid;
        }

        ship.Comp.Stage = CMUShipHijackStage.Sublight;
        var reactors = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (reactors.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid is { } map && ship.Comp.ShipMaps.Contains(map))
                EnsureComp<CMUReactorOverloadComponent>(uid);
        }
        ship.Comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(CMUHijackMath.TickSeconds);
        var progress = EnsureComp<EvacuationProgressComponent>(ship);
        progress.Progress = 0;
        progress.NextAnnounce = 25;
        progress.StartAnnounced = true;
        progress.SelfDestructAt = null;
        Dirty(ship.Owner, progress);
        Dirty(ship);
        RefreshPumps(ship, 0);
        Announce(ship, "cmu-hijack-start");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CMUShipHijackComponent>();
        while (query.MoveNext(out var uid, out var ship))
        {
            if (ship.Stage is CMUShipHijackStage.Idle or CMUShipHijackStage.Destroyed || Paused(uid))
                continue;
            ProcessTransitions((uid, ship));
            if (_timing.CurTime < ship.NextUpdate)
                continue;
            ship.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(CMUHijackMath.TickSeconds);
            if (ship.Stage is CMUShipHijackStage.Sublight or CMUShipHijackStage.FTL)
                AdvanceObjectives((uid, ship));
            else if (ship.SelfDestructUnlocked && ship.Stage is CMUShipHijackStage.FTLCrash or CMUShipHijackStage.GroundCrash)
                ProcessOverloads((uid, ship));
            if (ship.GroundImpacted && ship.Stage == CMUShipHijackStage.GroundCrash)
                ShortWreckApcs((uid, ship));
        }
    }

    private IEnumerable<Entity<AreaComponent>> GetAreas(Entity<CMUShipHijackComponent> ship)
    {
        var seen = new HashSet<EntityUid>();
        foreach (var grid in ship.Comp.ShipGrids)
        {
            if (!TryComp(grid, out AreaGridComponent? areaGrid))
                continue;
            foreach (var uid in areaGrid.AreaEntities.Values)
            {
                if (seen.Add(uid) && TryComp(uid, out AreaComponent? area) && area.HijackEvacuationArea)
                    yield return (uid, area);
            }
        }
    }

    public bool IsFuelAreaOperational(Entity<CMUShipHijackComponent> ship, EntityUid area)
    {
        if (ship.Comp.Pumps.TryGetValue(area, out var pump))
            return pump is { } uid && !TerminatingOrDeleted(uid) &&
                   TryComp(uid, out CMUHijackPumpComponent? fuelPump) && !fuelPump.Broken;
        return _power.IsAreaPowered(area, RMCPowerChannel.Equipment);
    }

    private void AdvanceObjectives(Entity<CMUShipHijackComponent> ship)
    {
        if (!TryComp(ship, out EvacuationProgressComponent? progress))
            return;

        if (ship.Comp.ObjectivesStopped)
            return;
        if (ship.Comp.CheckMarinePresence && !HasLivingCrew(ship))
        {
            ship.Comp.ObjectivesStopped = true;
            Announce(ship, "cmu-hijack-no-life-signs");
            return;
        }
        ProcessFTLExposure(ship);

        // Thresholds are checked before the next fuel tick, matching SShijack.fire.
        if (progress.Progress >= CMUHijackMath.CompleteProgress)
        {
            ship.Comp.InFTL = false;
            ship.Comp.Stage = CMUShipHijackStage.Arriving;
            ship.Comp.TransitionAt = _timing.CurTime + TimeSpan.FromSeconds(10);
            SetFlightVisuals(ship, false);
            Announce(ship, "cmu-hijack-arrived");
            Dirty(ship);
            return;
        }
        if (progress.Progress >= CMUHijackMath.FtlProgress && !ship.Comp.InFTL)
        {
            ship.Comp.InFTL = true;
            ship.Comp.Stage = CMUShipHijackStage.FTL;
            ship.Comp.TransitionAt = _timing.CurTime + TimeSpan.FromSeconds(5);
            Announce(ship, "cmu-hijack-ftl-charging");
            EntityManager.System<Content.Server._RMC14.Dropship.DropshipSystem>().DivertIncomingHijackFlights();
        }

        double additive = 0;
        double multiplicative = 1;
        foreach (var area in GetAreas(ship))
        {
            var online = IsFuelAreaOperational(ship, area);
            if (!progress.LastPower.TryGetValue(area, out var previous) || previous != online)
                Announce(ship, "cmu-hijack-area-state", ("area", Name(area)),
                    ("state", Loc.GetString(online ? "cmu-hijack-online" : "cmu-hijack-offline")));
            progress.LastPower[area] = online;
            if (!online)
                continue;
            if (area.Comp.HijackEvacuationType == AreaHijackEvacuationType.Add)
                additive += area.Comp.HijackEvacuationWeight;
            else
                multiplicative *= area.Comp.HijackEvacuationWeight;
        }

        ship.Comp.LastProgress = additive * multiplicative;
        if (ship.Comp.LastProgress <= 0)
        {
            BeginCrash(ship);
            return;
        }
        progress.Progress = Math.Min(100, progress.Progress + ship.Comp.LastProgress);
        if (progress.Progress >= progress.NextAnnounce)
        {
            var checkpoint = progress.NextAnnounce;
            progress.NextAnnounce += 25;
            Announce(ship, $"cmu-hijack-progress-{checkpoint}");
            if (!progress.IsHumanHijack)
            {
                var targets = string.Join(", ", progress.LastPower.Where(p => p.Value).Select(p => Name(p.Key)));
                _xenoAnnouncements.AnnounceAll(default, Loc.GetString("cmu-hijack-xeno-progress",
                    ("progress", checkpoint), ("areas", targets)));
            }
            var ev = new EvacuationProgressEvent(checkpoint, ship);
            RaiseLocalEvent(ship, ref ev, true);
            RefreshPumps(ship, checkpoint);
        }
        Dirty(ship.Owner, progress);
        Dirty(ship);
    }

    private void BeginCrash(Entity<CMUShipHijackComponent> ship)
    {
        if (ship.Comp.InFTL)
        {
            ship.Comp.Stage = CMUShipHijackStage.FTLCrash;
            ship.Comp.TransitionAt = _timing.CurTime + TimeSpan.FromSeconds(5);
            ship.Comp.SelfDestructUnlockAt = _timing.CurTime + TimeSpan.FromSeconds(30);
            Announce(ship, "cmu-hijack-ftl-failure");
        }
        else
        {
            ship.Comp.Stage = CMUShipHijackStage.GroundCrash;
            ship.Comp.TransitionAt = _timing.CurTime + TimeSpan.FromSeconds(20);
            PrepareGroundCrash(ship);
            Announce(ship, "cmu-hijack-ground-failure");
        }
        EntityManager.System<Content.Server._RMC14.Dropship.DropshipSystem>().DivertIncomingHijackFlights();
        Dirty(ship);
    }

    private void ProcessTransitions(Entity<CMUShipHijackComponent> ship)
    {
        ProcessPumpBlasts(ship);
        if (ship.Comp.Stage == CMUShipHijackStage.Detonating)
        {
            ProcessDetonation(ship);
            return;
        }
        if (ship.Comp.TransitionAt is { } at && _timing.CurTime >= at)
        {
            ship.Comp.TransitionAt = null;
            switch (ship.Comp.Stage)
            {
                case CMUShipHijackStage.FTL:
                    ship.Comp.FTLEnteredAt = _timing.CurTime;
                    SetFlightVisuals(ship, true);
                    Announce(ship, "cmu-hijack-ftl-entered");
                    break;
                case CMUShipHijackStage.FTLCrash:
                    ship.Comp.InFTL = false;
                    SetFlightVisuals(ship, false);
                    ship.Comp.PumpExplosionAt = _timing.CurTime + TimeSpan.FromSeconds(10);
                    WarnPumps(ship);
                    break;
                case CMUShipHijackStage.GroundCrash:
                    ImpactGround(ship);
                    if (ship.Comp.ContinueOnGroundCrash)
                        ship.Comp.SelfDestructUnlockAt = _timing.CurTime + TimeSpan.FromSeconds(15);
                    else if (!_distress.TryEndActiveDistressRound(DistressSignalRuleResult.MajorXenoVictory,
                                 "cmu-hijack-ground-result"))
                        _ticker.EndRound(Loc.GetString("cmu-hijack-ground-result"));
                    break;
                case CMUShipHijackStage.Arriving:
                    ship.Comp.Stage = CMUShipHijackStage.Docked;
                    Announce(ship, "cmu-hijack-docked");
                    break;
            }
            Dirty(ship);
        }
        if (ship.Comp.PumpExplosionAt is { } explode && _timing.CurTime >= explode)
        {
            ship.Comp.PumpExplosionAt = null;
            ExplodePumps(ship);
        }
        if (ship.Comp.SelfDestructUnlockAt is { } unlock && _timing.CurTime >= unlock)
        {
            ship.Comp.SelfDestructUnlockAt = null;
            if (!ship.Comp.AdminSelfDestructBlocked)
            {
                ship.Comp.SelfDestructUnlocked = true;
                Announce(ship, "cmu-hijack-self-destruct-unlocked");
                Dirty(ship);
            }
        }
    }

    private void RefreshPumps(Entity<CMUShipHijackComponent> ship, int progress)
    {
        var visual = progress switch
        {
            >= 100 => EvacuationPumpVisuals.Full,
            >= 75 => EvacuationPumpVisuals.SeventyFive,
            >= 50 => EvacuationPumpVisuals.Fifty,
            >= 25 => EvacuationPumpVisuals.TwentyFive,
            _ => EvacuationPumpVisuals.Empty,
        };
        foreach (var uid in ship.Comp.Pumps.Values)
        {
            if (uid is not { } pump || TerminatingOrDeleted(pump) ||
                !TryComp(pump, out EvacuationPumpComponent? comp) ||
                TryComp(pump, out CMUHijackPumpComponent? health) && health.Broken)
                continue;
            _appearance.SetData(pump, EvacuationPumpLayers.Layer, visual);
            _appearance.SetData(pump, PowerDeviceVisuals.Powered, true);
            _ambient.SetSound(pump, comp.ActiveSound);
        }
    }

    private void Announce(Entity<CMUShipHijackComponent> ship, string message, params (string, object)[] args)
        => _announcements.AnnounceARESStaging(null, Loc.GetString(message, args), faction: ship.Comp.VictimFaction);

    private Filter ShipFilter(Entity<CMUShipHijackComponent> ship)
    {
        var filter = Filter.Empty();
        foreach (var map in ship.Comp.ShipMaps)
        {
            if (!TerminatingOrDeleted(map))
                filter.AddPlayers(Filter.BroadcastMap(Transform(map).MapID).Recipients);
        }
        return filter;
    }

    private bool HasLivingCrew(Entity<CMUShipHijackComponent> ship)
    {
        var crew = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent>();
        while (crew.MoveNext(out _, out var marine, out var mob, out var transform))
        {
            if (mob.CurrentState != MobState.Dead && transform.MapUid is { } map && ship.Comp.ShipMaps.Contains(map) &&
                (ship.Comp.VictimFaction == null || marine.Faction == ship.Comp.VictimFaction))
                return true;
        }
        return false;
    }

    private void ProcessFTLExposure(Entity<CMUShipHijackComponent> ship)
    {
        if (!ship.Comp.InFTL || ship.Comp.FTLEnteredAt is not { } entered || _timing.CurTime - entered < TimeSpan.FromSeconds(30))
            return;
        var chance = (float) (Math.Clamp((_timing.CurTime - entered).TotalSeconds, 30, 300) - 30) / (2400 - 30);
        var crew = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent>();
        while (crew.MoveNext(out var uid, out _, out var mob, out var transform))
        {
            if (mob.CurrentState == MobState.Dead || transform.MapUid is not { } map || !ship.Comp.ShipMaps.Contains(map) ||
                HasComp<CryostorageContainedComponent>(uid) || HasComp<InsideCryoPodComponent>(uid) || !_random.Prob(chance))
                continue;
            EntityManager.System<SharedNeurotoxinSystem>().DoHallucination(uid);
        }
    }
}
