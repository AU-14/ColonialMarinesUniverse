using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// Keeps a vehicle's tyres from the wheel set fitted to it and works out what they do on the ground
/// under it, which the grid mover applies to its speed and its drag in water.
/// </summary>
public sealed class CMUVehicleTyreSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleTyresComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUVehicleTyresComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUVehicleTyresComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CMUVehicleTyresComponent, ExaminedEvent>(OnVehicleExamined);
        SubscribeLocalEvent<CMUVehicleTyreComponent, ExaminedEvent>(OnWheelsExamined);
    }

    private void OnMapInit(Entity<CMUVehicleTyresComponent> ent, ref MapInitEvent args)
    {
        Refresh(ent);
    }

    private void OnInserted(Entity<CMUVehicleTyresComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        Refresh(ent);
    }

    private void OnRemoved(Entity<CMUVehicleTyresComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        Refresh(ent);
    }

    private void OnVehicleExamined(Entity<CMUVehicleTyresComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Tyre is { } tyre && _prototypes.TryIndex(tyre, out var proto))
            args.PushMarkup(Loc.GetString(proto.Examine));
    }

    private void OnWheelsExamined(Entity<CMUVehicleTyreComponent> ent, ref ExaminedEvent args)
    {
        if (_prototypes.TryIndex(ent.Comp.Tyre, out var proto))
            args.PushMarkup(Loc.GetString(proto.Examine));
    }

    /// <summary>
    /// Takes the tyres from the first wheel slot holding a wheel set that has them.
    /// </summary>
    private void Refresh(Entity<CMUVehicleTyresComponent> ent)
    {
        // The server's state already carries the tyres; changing them while it applies breaks prediction.
        if (_timing.ApplyingState)
            return;

        ProtoId<CMUVehicleTyrePrototype>? tyre = null;
        if (TryComp(ent, out VehicleWheelSlotsComponent? wheels) && TryComp(ent, out ItemSlotsComponent? slots))
        {
            foreach (var id in wheels.Slots)
            {
                if (_itemSlots.TryGetSlot((ent, slots), id, out var slot) &&
                    TryComp(slot.Item, out CMUVehicleTyreComponent? fitted))
                {
                    tyre = fitted.Tyre;
                    break;
                }
            }
        }

        if (ent.Comp.Tyre == tyre)
            return;

        ent.Comp.Tyre = tyre;
        Dirty(ent);
    }

    public CMUVehicleTyrePrototype? GetTyre(EntityUid vehicle)
    {
        return TryComp(vehicle, out CMUVehicleTyresComponent? tyres) &&
               tyres.Tyre is { } tyre &&
               _prototypes.TryIndex(tyre, out var proto)
            ? proto
            : null;
    }

    /// <summary>
    /// The prefix of the art states for a wheel set's tyres, or for the tyres fitted to a vehicle.
    /// </summary>
    public string StatePrefix(EntityUid? uid)
    {
        ProtoId<CMUVehicleTyrePrototype>? tyre = null;
        if (TryComp(uid, out CMUVehicleTyreComponent? wheels))
            tyre = wheels.Tyre;
        else if (TryComp(uid, out CMUVehicleTyresComponent? fitted))
            tyre = fitted.Tyre;

        return tyre is { } id && _prototypes.TryIndex(id, out var proto) ? proto.StatePrefix : string.Empty;
    }

    /// <summary>
    /// Whether the tile under the vehicle is rough ground: one a shovel can dig.
    /// </summary>
    public bool OnRoughGround(EntityUid vehicle)
    {
        // Asked several times a tick (top speed, reverse, acceleration, grit), so read the tile once.
        TryComp(vehicle, out CMUVehicleTyresComponent? tyres);
        if (tyres != null && tyres.GroundTick == _timing.CurTick)
            return tyres.Rough;

        var rough = false;
        var xform = Transform(vehicle);
        if (xform.GridUid is { } grid &&
            TryComp(grid, out MapGridComponent? gridComp) &&
            _map.TryGetTileRef(grid, gridComp, xform.Coordinates, out var tile))
        {
            rough = _tiles[tile.Tile.TypeId] is ContentTileDefinition { CanDig: true };
        }

        if (tyres != null)
        {
            tyres.GroundTick = _timing.CurTick;
            tyres.Rough = rough;
        }

        return rough;
    }

    /// <summary>
    /// What the tyres make of the vehicle's top speed and acceleration on the ground under it.
    /// </summary>
    public float SpeedMultiplier(EntityUid vehicle)
    {
        if (GetTyre(vehicle) is not { } tyre)
            return 1f;

        return OnRoughGround(vehicle) ? tyre.RoughSpeed : tyre.PavedSpeed;
    }

    public float WaterDrag(EntityUid vehicle)
    {
        return GetTyre(vehicle)?.WaterDrag ?? 1f;
    }
}
