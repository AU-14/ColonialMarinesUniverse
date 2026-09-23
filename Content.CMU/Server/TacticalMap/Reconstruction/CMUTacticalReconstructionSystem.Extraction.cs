using System.Numerics;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Wall;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Water;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private readonly record struct Cell(byte Material, uint Appearance, byte Direction);

    /// <summary>Static anchored structures only, with no inventory or actor state.</summary>
    public byte ReadCell(EntityUid? map, Vector2i tile) => ReadDetails(map, tile, null).Material;

    private Cell ReadDetails(EntityUid? map, Vector2i tile, Atlas? atlas)
    {
        if (map is not { } uid || TerminatingOrDeleted(uid) || !TryComp<MapGridComponent>(uid, out var grid))
            return default;
        var floor = _maps.GetTileRef(uid, grid, tile).Tile;
        var material = CMUReconMaterial.Empty;
        ushort surface = 0;
        var direction = (byte) ((floor.RotationMirroring & 7) << 2);
        if (!floor.IsEmpty)
        {
            var definition = _tiles[floor.TypeId];
            material = definition is ContentTileDefinition { Weather: true } ? CMUReconMaterial.Ground : CMUReconMaterial.Floor;
            if (atlas != null)
                surface = Surface(atlas, definition.ID, floor.Variant, false);
        }
        var priority = 0;
        ushort detail = 0;
        var anchored = _maps.GetAnchoredEntities(uid, grid, tile);
        while (anchored.MoveNext(out var candidate))
        {
            if (candidate is not { } entity || TerminatingOrDeleted(entity))
                continue;
            if (HasComp<AreaComponent>(entity) || HasComp<AreaLabelComponent>(entity) || HasComp<RoofingEntityComponent>(entity))
                continue;
            var prototype = MetaData(entity).EntityPrototype?.ID;
            if (prototype == null)
                continue;
            var kind = Kind(prototype);
            if (TryComp<DoorComponent>(entity, out var door))
                kind = HasComp<CMDoubleDoorComponent>(entity)
                    ? door.State == DoorState.Open ? CMUReconMaterial.OpenDoubleDoor : CMUReconMaterial.DoubleDoor
                    : door.State == DoorState.Open ? CMUReconMaterial.OpenDoor : CMUReconMaterial.Door;
            else if (HasComp<RMCWaterComponent>(entity))
                kind = CMUReconMaterial.Water;
            else if (HasComp<WallComponent>(entity))
                kind = kind == CMUReconMaterial.Rock ? kind : CMUReconMaterial.Wall;
            else if (HasComp<BarricadeComponent>(entity))
                kind = CMUReconMaterial.Barricade;
            else if (HasComp<CMUZLevelHighGroundComponent>(entity))
                kind = CMUReconMaterial.Stairs;
            else if (kind == CMUReconMaterial.Empty && TryComp<PhysicsComponent>(entity, out var physics) &&
                     physics.CanCollide && (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
                kind = HasComp<OccluderComponent>(entity) ? CMUReconMaterial.Wall : CMUReconMaterial.Sprite;
            var score = Priority(kind);
            if (score <= priority)
                continue;
            priority = score;
            material = kind;
            var rotation = (int) Math.Round(Transform(entity).LocalRotation.Theta / (Math.PI / 2));
            direction = (byte) ((direction & ~3) | (rotation & 3));
            if (atlas != null)
                detail = Surface(atlas, prototype, door == null ? (byte) 0 : (byte) ((rotation & 3) | (door.State == DoorState.Open ? 4 : 0)), true);
        }
        return new Cell((byte) material, (uint) surface | ((uint) detail << 16), direction);
    }

    private static ushort Surface(Atlas atlas, string prototype, byte variant, bool entity)
    {
        var key = (prototype, variant, entity);
        if (atlas.SurfaceIds.TryGetValue(key, out var id))
            return id;
        if (atlas.Surfaces.Count >= CMUReconGeometry.MaxSurfaces - 1)
            return 0;
        id = (ushort) (atlas.Surfaces.Count + 1);
        atlas.SurfaceIds.Add(key, id);
        atlas.Surfaces.Add(new CMUReconSurface(id, prototype, variant, entity));
        return id;
    }

    private CMUReconMaterial Kind(string prototype)
    {
        if (_kinds.TryGetValue(prototype, out var kind))
            return kind;
        // Visual categories are cached per prototype, then overridden by authoritative components.
        // Unknown nonblocking decoration stays out of the structural model.
        var id = prototype.ToLowerInvariant();
        kind = id.Contains("tree") ? CMUReconMaterial.Tree :
            id.Contains("bush") || id.Contains("flora") || id.Contains("chair") || id.Contains("bench") || id.Contains("bed") ? CMUReconMaterial.Sprite :
            id.Contains("rock") || id.Contains("boulder") || id.Contains("mineable") ? CMUReconMaterial.Rock :
            id.Contains("window") ? CMUReconMaterial.Glass :
            id.Contains("railing") || id.Contains("fence") ? CMUReconMaterial.Railing :
            id.Contains("stair") ? CMUReconMaterial.Stairs :
            id.Contains("crate") || id.Contains("locker") || id.Contains("cabinet") ? CMUReconMaterial.Crate :
            id.Contains("table") ? CMUReconMaterial.Furniture :
            id.Contains("machine") || id.Contains("computer") || id.Contains("console") || id.Contains("generator") ||
            id.Contains("vendor") || id.Contains("pump") || id.Contains("tank") ? CMUReconMaterial.Machinery : CMUReconMaterial.Empty;
        _kinds[prototype] = kind;
        return kind;
    }

    private static int Priority(CMUReconMaterial material) => material switch
    {
        CMUReconMaterial.Wall => 100,
        CMUReconMaterial.Rock => 95,
        CMUReconMaterial.Door or CMUReconMaterial.DoubleDoor or CMUReconMaterial.OpenDoor or CMUReconMaterial.OpenDoubleDoor => 90,
        CMUReconMaterial.Glass => 80,
        CMUReconMaterial.Barricade or CMUReconMaterial.Railing => 70,
        CMUReconMaterial.Tree => 60,
        CMUReconMaterial.Machinery => 50,
        CMUReconMaterial.Crate => 40,
        CMUReconMaterial.Stairs => 30,
        CMUReconMaterial.Furniture => 20,
        CMUReconMaterial.Sprite => 15,
        CMUReconMaterial.Water => 10,
        _ => 0,
    };
}
