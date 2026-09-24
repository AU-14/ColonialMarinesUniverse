using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.util;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ops.ForceOnForce;

/// <summary>
///     Applies the OpforShipSwaps table to a scope: every Govfor-baked entity with a swap
///     entry is replaced by its Opfor twin, so the entity itself rather than just its
///     components ends up faction-correct. Shared by the platoon ship loop, dropship
///     loading, and opfor vehicle interiors.
/// </summary>
public sealed class FactionSwapSystem : EntitySystem
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private SharedSupplyDropSystem _supplyDrop = default!;

    private static readonly ProtoId<FactionSwapSetPrototype> OpforShipSwaps = "OpforShipSwaps";

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleEnterComponent, VehicleInteriorLoadedEvent>(OnVehicleInteriorLoaded);
    }

    private void OnVehicleInteriorLoaded(Entity<VehicleEnterComponent> ent, ref VehicleInteriorLoadedEvent args)
    {
        // A loaded interior map may have no grid; EnsureInterior still completes
        if (!args.Grid.IsValid())
            return;

        if (args.Faction != Team.OpFor)
            return;

        ConvertGovforGridToOpfor(args.Grid);
    }

    public bool IsMarkerOnShipOrZLevel(EntityUid shipUid, TransformComponent shipTransform, TransformComponent markerTransform)
    {
        if (markerTransform.ParentUid == shipUid || markerTransform.GridUid == shipUid)
            return true;

        if (shipTransform.MapUid is not { } shipMap
            || markerTransform.MapUid is not { } markerMap)
            return false;

        if (markerMap == shipMap)
            return false;

        if (!_zLevels.TryGetZNetwork(shipMap, out var shipNetwork)
            || !_zLevels.TryGetZNetwork(markerMap, out var markerNetwork))
            return false;

        return shipNetwork.Value.Owner == markerNetwork.Value.Owner;
    }

    public void ConvertGovforEntitiesToOpfor(EntityUid shipUid, TransformComponent shipTransform)
    {
        if (!_prototypeManager.TryIndex(OpforShipSwaps, out FactionSwapSetPrototype? swapSet))
            return;

        var toSwap = new List<(EntityUid uid, EntProtoId opforProtoId, TransformComponent transform)>();
        var supplyDrops = new List<EntityUid>();

        CollectFromGrid(shipUid, swapSet.Swaps, toSwap, supplyDrops);
        if (shipTransform.MapUid is { } shipMap
            && _zLevels.TryGetZNetwork(shipMap, out var network))
        {
            foreach (var map in network.Value.Comp.ZLevels.Values)
            {
                if (map is not { } mapUid || mapUid == shipMap)
                    continue;

                CollectFromGrid(mapUid, swapSet.Swaps, toSwap, supplyDrops);
            }
        }

        ApplySwaps(toSwap, supplyDrops);
    }

    public void ConvertGovforGridToOpfor(EntityUid grid)
    {
        if (!_prototypeManager.TryIndex(OpforShipSwaps, out FactionSwapSetPrototype? swapSet))
            return;

        var toSwap = new List<(EntityUid uid, EntProtoId opforProtoId, TransformComponent transform)>();
        var supplyDrops = new List<EntityUid>();
        CollectFromGrid(grid, swapSet.Swaps, toSwap, supplyDrops);
        ApplySwaps(toSwap, supplyDrops);
    }

    private void CollectSwap(
        EntityUid uid,
        MetaDataComponent meta,
        TransformComponent transform,
        Dictionary<string, EntProtoId> swaps,
        List<(EntityUid uid, EntProtoId opforProtoId, TransformComponent transform)> toSwap,
        List<EntityUid> supplyDrops)
    {
        if (meta.EntityPrototype is not { } proto)
            return;

        if (swaps.TryGetValue(proto.ID, out var opforProtoId))
            toSwap.Add((uid, opforProtoId, transform));
        else if (proto.ID == "RMCSupplyDropConsole")
            supplyDrops.Add(uid);
    }

    private void CollectFromGrid(
        EntityUid parent,
        Dictionary<string, EntProtoId> swaps,
        List<(EntityUid uid, EntProtoId opforProtoId, TransformComponent transform)> toSwap,
        List<EntityUid> supplyDrops)
    {
        var node = Transform(parent).ChildEnumerator;
        while (node.MoveNext(out var child))
        {
            CollectSwap(child, MetaData(child), Transform(child), swaps, toSwap, supplyDrops);
            CollectFromGrid(child, swaps, toSwap, supplyDrops);
        }
    }

    private void ApplySwaps(
        List<(EntityUid uid, EntProtoId opforProtoId, TransformComponent transform)> toSwap,
        List<EntityUid> supplyDrops)
    {
        foreach (var (uid, opforProtoId, transform) in toSwap)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(opforProtoId, out _))
                continue;

            _entityManager.SpawnAttachedTo(opforProtoId, transform.Coordinates, rotation: transform.LocalRotation);
            _entityManager.DeleteEntity(uid);
        }

        foreach (var uid in supplyDrops)
            _supplyDrop.SetSquad(uid, "SquadOpfor");
    }
}
