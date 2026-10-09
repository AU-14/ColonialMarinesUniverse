using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Rules;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.RMCLoreExaminable;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

/// <summary>
/// Materializes a custom plan in bounded batches on an uninitialized map. A dropship destination is
/// only published by OpenLandingZone after completion, so selection can never expose a partial map.
/// </summary>
public sealed partial class CMUExpeditionSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const int TileBudget = 1024;
    private const int ObjectBudget = 48;
    private const int MaxExpeditions = 3;
    private BuildJob? _job;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUExpeditionMapComponent, ComponentShutdown>(OnExpeditionShutdown);
    }

    public bool TryGenerateScenario(ProtoId<CMUExpeditionScenarioPrototype> scenarioId, int seed,
        out EntityUid mapUid, out string error)
    {
        mapUid = default;
        error = "cmu-expedition-invalid-scenario";
        if (!_prototypes.TryIndex(scenarioId, out var scenario))
            return false;

        foreach (var id in new[] { scenario.Orders, scenario.FieldNote, scenario.Recorder })
        {
            if (!_prototypes.TryIndex(id, out var entity) || entity.Abstract)
                return false;
        }

        if (!TryGenerate(scenario.Profile.Id, seed, scenario.Landform, scenario.Story, out mapUid, out error))
            return false;

        Comp<CMUExpeditionMapComponent>(mapUid).Scenario = scenarioId;
        _metadata.SetEntityName(mapUid, Loc.GetString("cmu-expedition-sector-map-name",
            ("sector", Loc.GetString(scenario.Name)), ("seed", seed)));
        return true;
    }

    public bool TryGenerate(EntProtoId<CMUExpeditionProfileComponent> profileId, int seed,
        CMUExpeditionLandform landform, CMUExpeditionStory story, out EntityUid mapUid, out string error)
    {
        mapUid = default;
        error = "cmu-expedition-invalid-profile";
        if (_job != null)
        {
            error = "cmu-expedition-busy";
            return false;
        }

        if (EntityQuery<CMUExpeditionMapComponent>().Count() >= MaxExpeditions)
        {
            error = "cmu-expedition-limit";
            return false;
        }

        if (!_prototypes.TryIndex(profileId, out var proto) ||
            !proto.TryComp<CMUExpeditionProfileComponent>(out var profile, EntityManager.ComponentFactory))
            return false;

        // Resolve every reference before allocating a map. Never modify a prototype's component data.
        var tilePalette = new Dictionary<CMUExpeditionTerrain, ITileDefinition>();
        foreach (var terrain in Enum.GetValues<CMUExpeditionTerrain>())
        {
            if (!profile.Tiles.TryGetValue(terrain, out var id) || !_tiles.TryGetDefinition(id, out var tile))
                return false;
            tilePalette.Add(terrain, tile);
        }
        foreach (var prop in Enum.GetValues<CMUExpeditionProp>())
        {
            if (prop == CMUExpeditionProp.None)
                continue;
            if (!profile.Props.TryGetValue(prop, out var variants) || variants.Count == 0 ||
                variants.Any(id => !_prototypes.TryIndex(id, out var entity) || entity.Abstract))
                return false;
        }
        foreach (var detail in Enum.GetValues<CMUExpeditionDetail>())
        {
            if (detail == CMUExpeditionDetail.None)
                continue;
            if (!profile.Details.TryGetValue(detail, out var variants) || variants.Count == 0 ||
                variants.Any(id => !_prototypes.TryIndex(id, out var entity) || entity.Abstract))
                return false;
        }
        foreach (var kind in Enum.GetValues<CMUExpeditionWaterKind>())
        {
            if (kind == CMUExpeditionWaterKind.None)
                continue;
            if (!profile.Water.TryGetValue(kind, out var id) || !_prototypes.TryIndex(id, out var protoWater) || protoWater.Abstract)
                return false;
        }
        if (!_prototypes.TryIndex(profile.LandingBeacon, out _) || !_prototypes.TryIndex(profile.Wildfire, out _))
            return false;

        CMUExpeditionPlan plan;
        try
        {
            plan = CMUExpeditionGenerator.Generate(seed, profile.Biome, landform, story, profile.Size);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException e)
        {
            Log.Warning($"No feasible expedition layout for seed {seed}: {e.Message}");
            error = "cmu-expedition-no-layout";
            return false;
        }

        foreach (var piece in plan.WreckObjects.Values)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(piece.Prototype, out var wreckPrototype) || wreckPrototype.Abstract)
                return false;
        }
        var wreckTiles = new Dictionary<string, ITileDefinition>();
        foreach (var tile in plan.WreckFloors.Values)
        {
            if (!_tiles.TryGetDefinition(tile.Prototype, out var definition))
                return false;
            wreckTiles[tile.Prototype] = definition;
        }

        mapUid = _map.CreateMap(runMapInit: false);
        try
        {
            var grid = EnsureComp<MapGridComponent>(mapUid);
            var expedition = AddComp<CMUExpeditionMapComponent>(mapUid);
            expedition.Plan = plan;
            expedition.Profile = profileId;
            var gravity = EnsureComp<GravityComponent>(mapUid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            Dirty(mapUid, gravity);
            var light = EnsureComp<MapLightComponent>(mapUid);
            light.AmbientLightColor = profile.AmbientLight;
            Dirty(mapUid, light);
            EnsureComp<RoofComponent>(mapUid);
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));
            _metadata.SetEntityName(mapUid, Loc.GetString("cmu-expedition-map-name", ("seed", seed)));
            _job = new BuildJob(mapUid, grid, plan, profile, tilePalette, wreckTiles);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Could not start expedition {seed}: {e}");
            QueueDel(mapUid);
            error = "cmu-expedition-failed";
            return false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_job is not { } job)
            return;
        if (Deleted(job.Map) || Terminating(job.Map))
        {
            _job = null;
            return;
        }

        try
        {
            if (job.TileCursor < job.Plan.Terrain.Length)
            {
                var batch = new List<(Vector2i, Tile)>(TileBudget);
                var end = Math.Min(job.TileCursor + TileBudget, job.Plan.Terrain.Length);
                while (job.TileCursor < end)
                {
                    var i = job.TileCursor++;
                    var x = i % job.Plan.Size;
                    var y = i / job.Plan.Size;
                    var tile = job.Tiles[job.Plan.Terrain[i]];
                    if (job.Plan.WreckFloors.TryGetValue(i, out var wreckFloor))
                    {
                        batch.Add((new Vector2i(x, y), new Tile(job.WreckTiles[wreckFloor.Prototype].TileId,
                            rotationMirroring: wreckFloor.Rotation)));
                        continue;
                    }
                    var variant = (byte) (CMUExpeditionGenerator.Hash(job.Plan.Seed, x, y, 100) % Math.Max(1, (int) tile.Variants));
                    batch.Add((new Vector2i(x, y), new Tile(tile.TileId, variant: variant)));
                }
                _map.SetTiles(job.Map, job.Grid, batch);
                return;
            }

            var spawned = 0;
            while (job.WaterCursor < job.Plan.WaterDepth.Length && spawned < ObjectBudget)
            {
                var i = job.WaterCursor++;
                var x = i % job.Plan.Size;
                var y = i / job.Plan.Size;
                var tile = CMUExpeditionGenerator.GetWaterTile(job.Plan, x, y);
                if (tile.Kind == CMUExpeditionWaterKind.None)
                    continue;
                var water = Spawn(job.Profile.Water[tile.Kind], new EntityCoordinates(job.Map, new Vector2(x + 0.5f, y + 0.5f)));
                _transform.SetLocalRotation(water, Angle.FromDegrees(tile.QuarterTurns * 90));
                spawned++;
            }
            if (job.WaterCursor < job.Plan.WaterDepth.Length)
                return;
            while (job.PropCursor < job.Plan.Props.Length && spawned < ObjectBudget)
            {
                var i = job.PropCursor++;
                var prop = job.Plan.Props[i];
                if (prop == CMUExpeditionProp.None)
                    continue;
                var x = i % job.Plan.Size;
                var y = i / job.Plan.Size;
                var palette = job.Profile.Props[prop];
                var id = palette[(int) (CMUExpeditionGenerator.Hash(job.Plan.Seed, x, y, 101) % (uint) palette.Count)];
                if (job.Plan.WreckObjects.TryGetValue(i, out var wreckObject))
                    id = wreckObject.Prototype;
                var entity = Spawn(id, new EntityCoordinates(job.Map, new Vector2(x + 0.5f, y + 0.5f)));
                if (job.Plan.WreckObjects.ContainsKey(i))
                    _transform.SetLocalRotation(entity, Angle.FromDegrees(wreckObject.QuarterTurns * 90));
                if (prop == CMUExpeditionProp.Recovery)
                    Comp<CMUExpeditionMapComponent>(job.Map).RecoveryTarget = entity;
                spawned++;
            }
            if (job.PropCursor < job.Plan.Props.Length)
                return;

            while (job.DetailCursor < job.Plan.Details.Length && spawned < ObjectBudget)
            {
                var i = job.DetailCursor++;
                var detail = job.Plan.Details[i];
                if (detail == CMUExpeditionDetail.None)
                    continue;
                var x = i % job.Plan.Size;
                var y = i / job.Plan.Size;
                var palette = job.Profile.Details[detail];
                var id = palette[(int) (CMUExpeditionGenerator.Hash(job.Plan.Seed, x, y, 102) % (uint) palette.Count)];
                var jitterX = ((int) (CMUExpeditionGenerator.Hash(job.Plan.Seed, x, y, 311) % 7) - 3) * 0.04f;
                var jitterY = ((int) (CMUExpeditionGenerator.Hash(job.Plan.Seed, x, y, 312) % 7) - 3) * 0.04f;
                var entity = Spawn(id, new EntityCoordinates(job.Map, new Vector2(x + 0.5f + jitterX, y + 0.5f + jitterY)));
                if (detail is CMUExpeditionDetail.LeafLitter or CMUExpeditionDetail.Ash or CMUExpeditionDetail.Litter)
                    _transform.SetLocalRotation(entity, Angle.FromDegrees(CMUExpeditionGenerator.Hash(job.Plan.Seed, x, y, 313) % 4 * 90));
                spawned++;
            }
            if (job.DetailCursor < job.Plan.Details.Length)
                return;

            var expedition = Comp<CMUExpeditionMapComponent>(job.Map);
            if (!BuildAirspace(job, expedition))
                return;
            if (expedition.Scenario is { } scenarioId)
                PlaceStory(job.Map, expedition, _prototypes.Index(scenarioId));

            EnsureComp<RMCPlanetComponent>(job.Map);
            _map.InitializeMap(job.Map);
            expedition.Ready = true;
            InitializeExpeditionLighting(job.Map, expedition);
            if (expedition.AutoOpen && OpenLandingZone(job.Map))
                AnnounceExpedition(job.Map, expedition);
            _job = null;
            var ev = new CMUExpeditionReadyEvent();
            RaiseLocalEvent(job.Map, ref ev);
            Log.Info($"Expedition ready: map {Comp<MapComponent>(job.Map).MapId}, seed {job.Plan.Seed}, {job.Plan.Biome}/{job.Plan.Landform}/{job.Plan.Story}");
        }
        catch (Exception e)
        {
            Log.Error($"Expedition generation failed for seed {job.Plan.Seed}: {e}");
            _job = null;
            QueueDel(job.Map);
        }
    }

    private void PlaceStory(EntityUid mapUid, CMUExpeditionMapComponent expedition, CMUExpeditionScenarioPrototype scenario)
    {
        var plan = expedition.Plan;
        // These site centers and objective approaches are reserved, dry, and connected by the planner.
        // Spawn before map initialization so Paper resolves its localized text normally.
        var landing = plan.LandingZone;
        var objective = plan.Objective;
        // Keep the orders outside the centered tactical dropship footprint.
        PlaceEvidence(scenario.Orders, new CMUExpeditionPoint(landing.X + CMUExpeditionPlan.LandingRadius - 1, landing.Y));
        PlaceEvidence(scenario.FieldNote, plan.Sites[2].Center);
        PlaceEvidence(scenario.Recorder, new CMUExpeditionPoint(objective.X + 1, objective.Y));
        if (expedition.RecoveryTarget is { } target)
        {
            var lore = EnsureComp<RMCLoreExaminableComponent>(target);
            lore.Content = scenario.TargetLore;
            Dirty(target, lore);
        }

        void PlaceEvidence(EntProtoId id, CMUExpeditionPoint point)
        {
            expedition.Evidence.Add(Spawn(id, new EntityCoordinates(mapUid, new Vector2(point.X + 0.5f, point.Y + 0.5f))));
        }
    }

    /// <summary>Called by explicit mission selection; idempotent and unavailable during generation.</summary>
    public bool OpenLandingZone(EntityUid mapUid)
    {
        if (!TryComp<CMUExpeditionMapComponent>(mapUid, out var expedition) || !expedition.Ready || Terminating(mapUid))
            return false;
        if (expedition.LandingBeacon is { } existing && !Deleted(existing) && !Terminating(existing))
            return true;
        if (!_prototypes.Index(expedition.Profile).TryComp<CMUExpeditionProfileComponent>(out var profile, EntityManager.ComponentFactory))
            return false;
        var center = expedition.Plan.LandingZone;
        expedition.LandingBeacon = Spawn(profile.LandingBeacon, new EntityCoordinates(mapUid, new Vector2(center.X + 0.5f, center.Y + 0.5f)));
        var name = expedition.Scenario is { } scenarioId
            ? Loc.GetString("cmu-expedition-sector-lz-name", ("sector", Loc.GetString(_prototypes.Index(scenarioId).Name)),
                ("seed", expedition.Plan.Seed))
            : Loc.GetString("cmu-expedition-lz-name", ("seed", expedition.Plan.Seed));
        _metadata.SetEntityName(expedition.LandingBeacon.Value, name);
        if (!expedition.FiresStarted)
        {
            expedition.FiresStarted = true;
            foreach (var point in expedition.Plan.FirePockets)
                Spawn(profile.Wildfire, new EntityCoordinates(mapUid, new Vector2(point.X + 0.5f, point.Y + 0.5f)));
        }
        return true;
    }

    public override void Shutdown()
    {
        _job = null;
        base.Shutdown();
    }

    private sealed class BuildJob(EntityUid map, MapGridComponent grid, CMUExpeditionPlan plan,
        CMUExpeditionProfileComponent profile, Dictionary<CMUExpeditionTerrain, ITileDefinition> tiles,
        Dictionary<string, ITileDefinition> wreckTiles)
    {
        public readonly EntityUid Map = map;
        public readonly MapGridComponent Grid = grid;
        public readonly CMUExpeditionPlan Plan = plan;
        public readonly CMUExpeditionProfileComponent Profile = profile;
        public readonly Dictionary<CMUExpeditionTerrain, ITileDefinition> Tiles = tiles;
        public readonly Dictionary<string, ITileDefinition> WreckTiles = wreckTiles;
        public int TileCursor;
        public int PropCursor;
        public int WaterCursor;
        public int DetailCursor;
        public int AirspaceCursor;
    }
}
