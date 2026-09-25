using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared._NC14.DayNightCycle;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.GameTicking;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Wieldable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSystem _station = default!;
    private EntityUid? _developmentAircraft;
    private EntityUid? _developmentEnemyAircraft;
    private EntityUid? _developmentSpawn;

    private void InitializeDevelopment()
    {
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnDevelopmentSpawn);
    }

    private void OnDevelopmentSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Handled || !_configuration.GetCVar(FighterCVars.Development) || _ticker.LobbyEnabled)
            return;
        if (_developmentSpawn is not { } spawn || TerminatingOrDeleted(spawn))
        {
            if (_station.GetLargestGrid(args.Station) is not { } grid ||
                !TryComp(grid, out MapGridComponent? mapGrid))
                return;
            _developmentSpawn = Spawn("SpawnPointPassenger", new EntityCoordinates(grid, mapGrid.LocalAABB.Center));
        }
        args.Handled = true;
        _ticker.DoSpawn(args.Player, args.Profile, args.Station, "Passenger", true, out var mob, out _, out _);
        BoardDevelopmentPlayer(mob, args.Player.Name);
    }

    private void BoardDevelopmentPlayer(EntityUid mob, string playerName)
    {
        var airCombat = _configuration.GetCVar(FighterCVars.AirCombatTrial);
        var enemy = airCombat && playerName.EndsWith("EnemyPilot", StringComparison.Ordinal);
        EnsureComp<MarineComponent>(mob).Faction = enemy ? "opfor" : "govfor";
        var selected = enemy ? _developmentEnemyAircraft : _developmentAircraft;
        if (selected is not { } existing || TerminatingOrDeleted(existing))
        {
            var transform = Transform(mob);
            if (transform.MapUid is not { } map)
                return;
            var home = _transform.GetWorldPosition(mob);
            if (transform.GridUid is { } grid && TryComp(grid, out MapGridComponent? mapGrid))
                home = Vector2.Transform(mapGrid.LocalAABB.Center, _transform.GetWorldMatrix(grid));
            var created = _configuration.GetCVar(FighterCVars.GroundTrial)
                ? CreateGroundDevelopmentAircraft(map, home) : CreateAircraft(map, home);
            selected = created.Owner;
            if (enemy) _developmentEnemyAircraft = selected;
            else _developmentAircraft = selected;
            if (TryComp(created, out FighterWeaponsComponent? weapons))
            {
                weapons.Faction = enemy ? "opfor" : "govfor";
                if (created.Comp.GroundEntity is { } hull)
                    SetEquipmentFaction((hull, Comp<FighterIFFComponent>(hull)), weapons.Faction);
                Dirty(created.Owner, weapons);
            }
            EquipDevelopmentAircraft(created, !enemy);
            // Start the isolated visual trial in daylight; operational maps retain their lighting.
            foreach (var terrain in new[] { created.Comp.TerrainMap, created.Comp.ViewMap })
            {
                _map.SetAmbientLight(Comp<MapComponent>(terrain).MapId, Color.FromHex("#B9C9CE"));
                if (!TryComp(terrain, out DayNightCycleComponent? cycle))
                    continue;
                cycle.CurrentCycleTime = .5f;
                Dirty(terrain, cycle);
            }
        }
        var aircraft = new Entity<FighterAircraftComponent>(selected.Value, Comp<FighterAircraftComponent>(selected.Value));
        var observer = playerName.EndsWith("Observer", StringComparison.Ordinal);
        if (!Board(mob, aircraft, observer) && !Board(mob, aircraft, !observer))
            _transform.SetCoordinates(mob, new EntityCoordinates(aircraft, new Vector2(.5f, -0.5f)));
        Log.Info($"Fighter development player {playerName} boarded {ToPrettyString(aircraft.Owner)}.");
    }

    private void EquipDevelopmentAircraft(Entity<FighterAircraftComponent> aircraft, bool practiceFlares = true)
    {
        var weapons = Comp<FighterWeaponsComponent>(aircraft);
        var missiles = new[] { "RMCDropshipAttachmentAmmoRocketKeeper", "RMCDropshipAttachmentAmmoRocketWidowmaker",
            "RMCDropshipAttachmentAmmoRocketHarpoon", "RMCDropshipAttachmentAmmoRocketNapalm" };
        for (var i = 0; i < weapons.Hardpoints.Count; i++)
        {
            var uid = weapons.Hardpoints[i];
            var point = new Entity<FighterHardpointComponent>(uid, Comp<FighterHardpointComponent>(uid));
            if (i is 2 or 5)
                _hardpoints.TryMount(point, Spawn("RMCDropshipAttachmentRocketPod", Transform(uid).Coordinates));
            var ammo = i == FighterWeaponsComponent.GauSlot ? "RMCDropshipAttachmentAmmoGAU"
                : i is 2 or 5 ? "RMCDropshipAttachmentAmmoRocketMiniMike" : missiles[i < 2 ? i : i - 1];
            _hardpoints.TryMount(point, Spawn(ammo, Transform(uid).Coordinates));
        }
        // Only the opt-in trial supplies long-burning practice designations.
        // Operational flares still use their ordinary lifetime and deployment rules.
        var count = 0;
        for (var offset = -60; practiceFlares && offset <= 60 && count < 3; offset += 12)
        {
            var position = aircraft.Comp.Home + new Vector2(0, offset);
            var coordinates = new EntityCoordinates(aircraft.Comp.TerrainMap, position);
            if (!_areas.CanCAS(coordinates)) continue;
            var flare = Spawn("CMUFighterTrialFlare", coordinates);
            EntityManager.System<ExpendableLightSystem>().TryActivate((flare, Comp<ExpendableLightComponent>(flare)));
            _payloads.MakeDropshipTarget(flare, $"TEST-{++count}", weapons.Faction);
        }
        RefreshWeapons(aircraft, weapons);
        if (_configuration.GetCVar(FighterCVars.ManpadTrial) && weapons.Targets.Count > 0)
        {
            weapons.Faction = "govfor";
            Dirty(aircraft.Owner, weapons);
            var position = weapons.Targets[^1].Position + new Vector2(2, 0);
            var coordinates = new EntityCoordinates(aircraft.Comp.TerrainMap, position);
            var operatorUid = Spawn("MobHuman", coordinates);
            EnsureComp<MarineComponent>(operatorUid).Faction = "opfor";
            var launcher = Spawn("CMUFighterManpad", coordinates);
            var ammunition = Spawn("CMURocket70mm", coordinates);
            EntityManager.System<SharedGunSystem>().TryBallisticInsert(
                (launcher, Comp<BallisticAmmoProviderComponent>(launcher)), ammunition, operatorUid);
            var manpad = Comp<FighterManpadComponent>(launcher);
            var hands = EntityManager.System<SharedHandsSystem>();
            var wielding = EntityManager.System<SharedWieldableSystem>();
            var actions = EntityManager.System<SharedActionsSystem>();
            if (hands.TryPickupAnyHand(operatorUid, launcher) && wielding.TryWield(launcher, operatorUid) &&
                actions.GetAction(manpad.AimAction) is { } action)
                actions.PerformAction(operatorUid, action);
            Log.Info($"MANPAD preview ready at {position}, sector {FighterAirCombat.SectorAt(aircraft.Comp.Battlefield, position)}.");
        }
        Log.Info($"Fighter trial armed: six pylons and internal GAU, {count} flare designations.");
    }

    private Entity<FighterAircraftComponent> CreateGroundDevelopmentAircraft(EntityUid map, Vector2 origin)
    {
        while (TryComp(map, out CMUZLevelMapComponent? level) && level.Depth > 0 && level.MapBelow is { } below)
            map = below;
        var hull = Spawn("CMUFighterGround", new EntityCoordinates(map, origin));
        for (var radius = 0; radius <= 160; radius += 8)
        for (var x = -radius; x <= radius; x += 8)
        for (var y = -radius; y <= radius; y += 8)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
            var site = new EntityCoordinates(map, origin + new Vector2(x, y));
            if (!GroundSiteClear(hull, site)) continue;
              _transform.SetCoordinates(hull, site);
              Spawn("CMUFighterLandingPadGovfor", site);
            var uid = Comp<FighterGroundComponent>(hull).Aircraft!.Value;
            Log.Info($"Ground fighter trial ready at {site}.");
            return (uid, Comp<FighterAircraftComponent>(uid));
        }
        QueueDel(hull);
        throw new InvalidOperationException("No clear ground fighter trial site found on this map.");
    }
}
