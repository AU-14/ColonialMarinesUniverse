// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the farming of crystall-punk-14 (MIT / CC-BY-SA) and adapted to this project.
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Farming;

// Farming, shared by both sides: planting, harvesting and the state of a plant; the growth itself runs on the server.
public abstract partial class SharedFarmingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    protected EntityQuery<FarmPlantComponent> PlantQuery;
    protected EntityQuery<FarmPlanterSoilComponent> PlanterQuery;

    public override void Initialize()
    {
        base.Initialize();

        PlantQuery = GetEntityQuery<FarmPlantComponent>();
        PlanterQuery = GetEntityQuery<FarmPlanterSoilComponent>();

        SubscribeLocalEvent<FarmPlantComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<FarmSeedComponent, AfterInteractEvent>(OnSeedInteract);
        SubscribeLocalEvent<FarmSeedComponent, PlantSeedDoAfterEvent>(OnSeedPlantedDoAfter);
        SubscribeLocalEvent<FarmGatherableComponent, InteractUsingEvent>(OnGatherUse);
        SubscribeLocalEvent<FarmGatherableComponent, PlantGatherDoAfterEvent>(OnGatherDoAfter);
    }

    private void OnExamine(Entity<FarmPlantComponent> plant, ref ExaminedEvent args)
    {
        if (plant.Comp.Energy <= 0)
            args.PushMarkup(Loc.GetString("au14-farming-low-energy"));

        if (plant.Comp.Resource <= 0)
            args.PushMarkup(Loc.GetString("au14-farming-low-resources"));
    }

    public void AffectEnergy(Entity<FarmPlantComponent> ent, float energyDelta)
    {
        if (energyDelta == 0)
            return;

        ent.Comp.Energy = MathHelper.Clamp(ent.Comp.Energy + energyDelta, 0, ent.Comp.EnergyMax);
        Dirty(ent);
    }

    public void AffectResource(Entity<FarmPlantComponent> ent, float resourceDelta)
    {
        if (resourceDelta == 0)
            return;

        ent.Comp.Resource = MathHelper.Clamp(ent.Comp.Resource + resourceDelta, 0, ent.Comp.ResourceMax);
        Dirty(ent);
    }

    public void AffectGrowth(Entity<FarmPlantComponent> ent, float growthDelta)
    {
        if (growthDelta == 0)
            return;

        ent.Comp.GrowthLevel = MathHelper.Clamp01(ent.Comp.GrowthLevel + growthDelta);
        Dirty(ent);
    }

    // ---------------------------------------------------------------- the gathering
    private void OnGatherUse(Entity<FarmGatherableComponent> gatherable, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_whitelist.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.Used))
            return;

        args.Handled = TryHarvestPlant(gatherable, args.Used, args.User);
    }

    private bool CanHarvestPlant(Entity<FarmGatherableComponent> gatherable)
    {
        if (PlantQuery.TryComp(gatherable, out var plant) && plant.GrowthLevel < gatherable.Comp.GrowthLevelToHarvest)
            return false;

        return gatherable.Comp.Loot != null;
    }

    private bool TryHarvestPlant(Entity<FarmGatherableComponent> gatherable, EntityUid used, EntityUid user)
    {
        if (!CanHarvestPlant(gatherable))
            return false;

        _audio.PlayPvs(gatherable.Comp.GatherSound, Transform(gatherable).Coordinates);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, gatherable.Comp.GatherDelay, new PlantGatherDoAfterEvent(), gatherable, used: used)
        {
            BreakOnDamage = true,
            BlockDuplicate = false,
            CancelDuplicate = false,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        return true;
    }

    private void OnGatherDoAfter(Entity<FarmGatherableComponent> gatherable, ref PlantGatherDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !CanHarvestPlant(gatherable))
            return;

        args.Handled = true;
        HarvestPlant(gatherable, out _, args.Used);
    }

    public void HarvestPlant(Entity<FarmGatherableComponent> gatherable, out HashSet<EntityUid> result, EntityUid? used)
    {
        result = new();

        if (_net.IsClient || gatherable.Comp.Loot == null)
            return;

        var pos = _transform.GetMapCoordinates(gatherable);

        foreach (var (tag, table) in gatherable.Comp.Loot)
        {
            if (tag != "All" && used != null && !_tag.HasTag(used.Value, tag))
                continue;

            foreach (var loot in _entityTable.GetSpawns(table))
            {
                var spawnPos = pos.Offset(_random.NextVector2(gatherable.Comp.GatherOffset));
                result.Add(Spawn(loot, spawnPos));
            }
        }

        _audio.PlayPvs(gatherable.Comp.GatherSound, Transform(gatherable).Coordinates);

        if (gatherable.Comp.DeleteAfterHarvest)
            _destructible.DestroyEntity(gatherable);
        else if (PlantQuery.TryComp(gatherable, out var plant))
            AffectGrowth((gatherable, plant), -gatherable.Comp.GrowthCostHarvest);
    }

    // ---------------------------------------------------------------- the planting
    private void OnSeedInteract(Entity<FarmSeedComponent> seed, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !CanPlantSeed(seed, args.ClickLocation, args.User))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, seed.Comp.PlantingTime, new PlantSeedDoAfterEvent(GetNetCoordinates(args.ClickLocation)), seed)
        {
            BreakOnDamage = true,
            BlockDuplicate = false,
            CancelDuplicate = false,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnSeedPlantedDoAfter(Entity<FarmSeedComponent> ent, ref PlantSeedDoAfterEvent args)
    {
        if (_net.IsClient || args.Handled || args.Cancelled)
            return;

        var position = GetCoordinates(args.Coordinates);
        if (!CanPlantSeed(ent, position, args.User))
            return;

        args.Handled = true;

        // In a planter the plant stands at the planter; on the ground, in the middle of the tile.
        Spawn(ent.Comp.PlantProto, position);
        QueueDel(ent);
    }

    public bool CanPlantSeed(Entity<FarmSeedComponent> seed, EntityCoordinates position, EntityUid? user)
    {
        var map = _transform.GetGrid(position) ?? _transform.GetMap(position);
        if (!TryComp<MapGridComponent>(map, out var gridComp))
            return false;

        var anchored = _map.GetAnchoredEntities((map.Value, gridComp), position);

        // A filled planter is soil of its own.
        var planter = false;
        foreach (var uid in anchored)
        {
            if (PlanterQuery.TryComp(uid, out var soil) && soil.Filled)
                planter = true;
        }

        if (!planter)
        {
            var tile = _turf.GetContentTileDefinition(_map.GetTileRef(map.Value, gridComp, position));
            if (!seed.Comp.SoilTile.Contains(tile))
            {
                if (user is not null && _timing.IsFirstTimePredicted && _net.IsClient)
                    _popup.PopupEntity(Loc.GetString("au14-farming-soil-wrong", ("seed", MetaData(seed).EntityName)), user.Value, user.Value);

                return false;
            }
        }

        foreach (var uid in anchored)
        {
            if (!PlantQuery.HasComp(uid))
                continue;

            if (user is not null && _timing.IsFirstTimePredicted && _net.IsClient)
                _popup.PopupEntity(Loc.GetString("au14-farming-soil-occupied"), user.Value, user.Value);

            return false;
        }

        return true;
    }

    [Serializable, NetSerializable]
    public sealed partial class PlantSeedDoAfterEvent : DoAfterEvent
    {
        [DataField(required: true)]
        public NetCoordinates Coordinates;

        public PlantSeedDoAfterEvent(NetCoordinates coordinates)
        {
            Coordinates = coordinates;
        }

        public override DoAfterEvent Clone() => this;
    }

    [Serializable, NetSerializable]
    public sealed partial class PlantGatherDoAfterEvent : SimpleDoAfterEvent;
}
