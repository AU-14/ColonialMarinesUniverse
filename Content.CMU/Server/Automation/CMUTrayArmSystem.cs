using Content.Server.CMU14.Botany;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.CMU14.Automation;
using Content.Shared.CMU14.Botany;
using Content.Shared.CMU14.Electric;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Automation;

/// <summary>
/// CMU14 (ARCH-03): the harvester and planter arms. Each act, a powered, anchored arm works one hydroponics tray of the 3x3
/// block in front of it (ECS-01, see <see cref="GetWorkTiles"/>). Trays with an <see cref="AutoFeedTrayComponent"/> farm themselves and are skipped (E1); the Ligneous tool check is
/// ignored, as the automatic tray does (E2).
/// </summary>
public sealed class CMUTrayArmSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly CMUTrayFarmingSystem _farming = default!;
    [Dependency] private readonly CMUSeedGenesSystem _genes = default!;
    [Dependency] private readonly PlantHarvestSystem _harvest = default!;
    [Dependency] private readonly PlantHolderSystem _holder = default!;
    [Dependency] private readonly CMUInserterSystem _inserter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PlantSystem _plants = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PlantTraySystem _tray = default!;

    private readonly List<EntityUid> _found = new();

    // ECS-04: the last produce the latest sweep buffered, which the claw shows coming back.
    private EntProtoId? _swept;

    // ECS-01/ECS-03: the arm faces left or right only and works the 3x3 block of tiles on that side: it stands at the middle of
    // the block's near edge, so the block is one to three tiles away and one tile up and down. The tile behind it stays free for
    // an inserter or a belt.
    public const int WorkDepth = 3;
    private readonly List<Vector2i> _workTiles = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUTrayArmComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CMUTrayArmComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUTrayArmComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<CMUTrayArmComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<CMUTrayArmComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUTrayArmComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CMUTrayArmComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnAnchorChanged(Entity<CMUTrayArmComponent> arm, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            SnapFacing(arm, args.Transform);

        if (!args.Anchored && !args.Detaching)
            DropBuffer(arm);
    }

    private void OnTerminating(Entity<CMUTrayArmComponent> arm, ref EntityTerminatingEvent args)
    {
        // When the grid or map goes with the arm, the buffer goes with it: nothing may be parented to a terminating entity.
        var xform = Transform(arm);
        if (TerminatingOrDeleted(xform.ParentUid) || (xform.MapUid is { } map && TerminatingOrDeleted(map)))
            return;

        DropBuffer(arm);
    }

    private void DropBuffer(Entity<CMUTrayArmComponent> arm)
    {
        var coords = Transform(arm).Coordinates;
        DropSlots(arm, arm.Comp.SeedSlots, coords);
        DropSlots(arm, arm.Comp.ProduceSlots, coords);
    }

    private void DropSlots(EntityUid arm, List<string> slots, EntityCoordinates coords)
    {
        foreach (var id in slots)
        {
            if (_itemSlots.TryGetSlot(arm, id, out var slot) && slot.ContainerSlot is { } container)
                _containers.EmptyContainer(container, force: true, destination: coords);
        }
    }

    private void OnExamined(Entity<CMUTrayArmComponent> arm, ref ExaminedEvent args)
    {
        // ECS-03: the side it works, as the work tiles count it (East or West).
        var side = WorkDirection(Transform(arm).LocalRotation) == Direction.East ? 1 : 3;
        var key = arm.Comp.Mode == CMUTrayArmMode.Harvest ? "cmu-tray-arm-examine-harvest" : "cmu-tray-arm-examine-plant";
        args.PushMarkup(Loc.GetString(key, ("side", Loc.GetString($"au14-belt-side-{side}"))));

        if (arm.Comp.Mode == CMUTrayArmMode.Plant && arm.Comp.Filter.Species.Count > 0)
            args.PushMarkup(Loc.GetString("cmu-tray-arm-examine-filter", ("count", arm.Comp.Filter.Species.Count)));
    }

    // COUPLING-02: a planter's species filter is set in the round by holding a seed packet; an empty filter plants anything.
    private void OnGetAlternativeVerbs(Entity<CMUTrayArmComponent> arm, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || arm.Comp.Mode != CMUTrayArmMode.Plant)
            return;

        var user = args.User;
        if (args.Using is { } held && TryComp<SeedComponent>(held, out var seed))
        {
            var species = seed.PlantProtoId;
            if (arm.Comp.Filter.Species.Contains(species))
                return;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-tray-arm-verb-filter-add"),
                IconEntity = GetNetEntity(held),
                Act = () =>
                {
                    arm.Comp.Filter.Species.Add(species);
                    _popup.PopupEntity(Loc.GetString("cmu-tray-arm-filter-added", ("seed", held)), arm, user);
                },
            });
            return;
        }

        if (args.Using != null)
            return;

        if (arm.Comp.Filter.Species.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-tray-arm-verb-filter-clear"),
                Act = () =>
                {
                    arm.Comp.Filter.Species.Clear();
                    _popup.PopupEntity(Loc.GetString("cmu-tray-arm-filter-cleared"), arm, user);
                },
            });
        }

        // The seed slots are stack slots (inserters only fill them), so a hand takes the packets back out, e.g. after the
        // filter changed and the loaded seeds no longer pass it.
        if (FirstSeed(arm) != null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-tray-arm-verb-take-seeds"),
                Act = () =>
                {
                    foreach (var id in arm.Comp.SeedSlots)
                    {
                        if (_itemSlots.TryGetSlot(arm.Owner, id, out var slot) && slot.HasItem)
                            _itemSlots.TryEjectToHands(arm.Owner, slot, user, true);
                    }
                },
            });
        }
    }

    // COUPLING-02: a planter refuses seed packets its filter refuses, so an inserter feeding it from a mixed source never jams it.
    private void OnInsertAttempt(Entity<CMUTrayArmComponent> arm, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID is not { } id || !arm.Comp.SeedSlots.Contains(id))
            return;

        if (!TryComp<SeedComponent>(args.Item, out var seed) || !_genes.Matches(arm.Comp.Filter, seed))
            args.Cancelled = true;
    }

    private EntityUid? FirstSeed(Entity<CMUTrayArmComponent> arm)
    {
        foreach (var id in arm.Comp.SeedSlots)
        {
            if (_itemSlots.GetItemOrNull(arm.Owner, id) is { } seed)
                return seed;
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUTrayArmComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arm, out var xform))
        {
            if (!arm.Enabled || !xform.Anchored || now < arm.NextAct)
                continue;

            arm.NextAct = now + arm.Interval;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            Act((uid, arm), xform);
        }
    }

    private void Act(Entity<CMUTrayArmComponent> arm, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        GetWorkTiles(tile, xform.LocalRotation, _workTiles);

        if (arm.Comp.Mode == CMUTrayArmMode.Plant)
        {
            foreach (var work in _workTiles)
            {
                if (FindTray(gridUid, grid, work) is not { } tray || !Plant(arm, tray, out var planted))
                    continue;

                Reach(arm, work - tile, planted, carriesOut: true);
                return;
            }

            return;
        }

        // Buffer what is already lying on the arm's tile first; a new harvest waits until the last one is all buffered, so
        // a slow inserter makes the arm wait instead of piling produce on the floor.
        if (!Sweep(arm, gridUid, grid, tile))
            return;

        foreach (var work in _workTiles)
        {
            if (FindTray(gridUid, grid, work) is not { } tray || Harvest(arm, tray) is not { } harvested)
                continue;

            EntProtoId? produce = null;
            if (harvested)
            {
                Sweep(arm, gridUid, grid, tile);
                produce = _swept;
            }

            Reach(arm, work - tile, produce, carriesOut: false);
            return;
        }
    }

    /// <summary>
    /// The tiles of the 3x3 block on the side an arm on <paramref name="tile"/> works (East or West, see <see cref="WorkDirection"/>),
    /// nearest row first and the same side first in each row, so one tray is worked an act in a
    /// fixed order that does not depend on entity ids.
    /// </summary>
    public static void GetWorkTiles(Vector2i tile, Angle rotation, List<Vector2i> tiles)
    {
        tiles.Clear();
        var forward = WorkDirection(rotation).ToIntVec();
        var side = new Vector2i(forward.Y, -forward.X);
        for (var depth = 1; depth <= WorkDepth; depth++)
        {
            for (var across = -1; across <= 1; across++)
                tiles.Add(tile + forward * depth + side * across);
        }
    }

    /// <summary>ECS-03: the side the arm works, East or West. A facing that is not East counts as West.</summary>
    public static Direction WorkDirection(Angle rotation)
    {
        return rotation.GetCardinalDir() == Direction.East ? Direction.East : Direction.West;
    }

    /// <summary>ECS-03: the art only reaches left or right, so a north or south facing (a frame or a wrench can give one) turns east.</summary>
    private void SnapFacing(EntityUid uid, TransformComponent xform)
    {
        var dir = xform.LocalRotation.GetCardinalDir();
        if (dir is Direction.East or Direction.West)
            return;

        _transform.SetLocalRotation(uid, Direction.East.ToAngle(), xform);
    }

    private void OnMapInit(Entity<CMUTrayArmComponent> arm, ref MapInitEvent args)
    {
        SnapFacing(arm, Transform(arm));
    }

    // ECS-03: the rotate verbs' "reset" turns an anchored arm south without re-anchoring it.
    private void OnMove(Entity<CMUTrayArmComponent> arm, ref MoveEvent args)
    {
        if (args.OldRotation != args.NewRotation && args.Component.Anchored)
            SnapFacing(arm, args.Component);
    }

    /// <summary>ECS-04: records the tray just worked so clients animate the arm reaching it.</summary>
    private void Reach(Entity<CMUTrayArmComponent> arm, Vector2i offset, EntProtoId? carries, bool carriesOut)
    {
        arm.Comp.ReachTarget = offset;
        arm.Comp.ReachStartedAt = _timing.CurTime;
        arm.Comp.ReachCarries = carries;
        arm.Comp.ReachCarriesOut = carriesOut;
        Dirty(arm);
    }

    private Entity<PlantTrayComponent>? FindTray(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        _inserter.FindAt(gridUid, grid, tile, _found);
        foreach (var uid in _found)
        {
            // E1: the automatic tray farms itself.
            if (TryComp<PlantTrayComponent>(uid, out var tray) && !HasComp<AutoFeedTrayComponent>(uid))
                return (uid, tray);
        }

        return null;
    }

    /// <returns>Null when the tray needed nothing; false when a dead plant was cleared away; true when the plant was harvested.</returns>
    private bool? Harvest(Entity<CMUTrayArmComponent> arm, Entity<PlantTrayComponent> tray)
    {
        if (!_tray.TryGetPlant((tray.Owner, tray.Comp), out var plant) || !TryComp<PlantHolderComponent>(plant, out var holder))
            return null;

        if (_holder.IsDead(plant.Value))
        {
            _plants.RemovePlant(plant.Value);
            return false;
        }

        // The produce spawns at the arm's own tile and is swept into the buffer, so there must be room for at least some of it.
        if (!holder.ReadyForHarvest || FreeSlot(arm, arm.Comp.ProduceSlots) == null)
            return null;

        // A plant can be ready with nothing to give (no yield or no products), and then the harvest does nothing; that tray
        // must not take every act from the rest of the block.
        _harvest.TryHandleHarvest(plant.Value, arm);
        return TerminatingOrDeleted(plant.Value) || !holder.ReadyForHarvest ? true : null;
    }

    /// <summary>Buffers the loose produce items on the arm's tile.</summary>
    /// <returns>True when no loose produce is left on the tile.</returns>
    private bool Sweep(Entity<CMUTrayArmComponent> arm, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        _inserter.FindAt(gridUid, grid, tile, _found);
        _swept = null;
        var clear = true;
        foreach (var item in _found)
        {
            // Some "produce" is a mob (killer tomatoes): only items are buffered, and a mob does not block the next harvest.
            if (!HasComp<ProduceComponent>(item) || !HasComp<ItemComponent>(item) || HasComp<MobStateComponent>(item) ||
                Transform(item).Anchored)
            {
                continue;
            }

            if (FreeSlot(arm, arm.Comp.ProduceSlots) is not { } slot || !_itemSlots.TryInsert(arm.Owner, slot, item, null))
                clear = false;
            else
                _swept = MetaData(item).EntityPrototype?.ID;
        }

        return clear;
    }

    /// <returns>True when a seed was planted; <paramref name="planted"/> is the seed packet's prototype, for the reach animation.</returns>
    private bool Plant(Entity<CMUTrayArmComponent> arm, Entity<PlantTrayComponent> tray, out EntProtoId? planted)
    {
        planted = null;
        if (_tray.TryGetPlant((tray.Owner, tray.Comp), out _))
            return false;

        foreach (var id in arm.Comp.SeedSlots)
        {
            if (_itemSlots.GetItemOrNull(arm.Owner, id) is not { } seed
                || TerminatingOrDeleted(seed)
                || EntityManager.IsQueuedForDeletion(seed)
                || !TryComp<SeedComponent>(seed, out var seedComp)
                || !_genes.Matches(arm.Comp.Filter, seedComp))
                continue;

            // One plant an act. The planting handler deletes the seed; a seed the filter refuses stays in its slot.
            var proto = MetaData(seed).EntityPrototype?.ID;
            if (_farming.TryPlantSeed((tray.Owner, tray.Comp), seed, arm) != null)
            {
                planted = proto;
                return true;
            }
        }

        return false;
    }

    private string? FreeSlot(EntityUid arm, List<string> slots)
    {
        foreach (var id in slots)
        {
            if (_itemSlots.TryGetSlot(arm, id, out var slot) && !slot.HasItem)
                return id;
        }

        return null;
    }
}
