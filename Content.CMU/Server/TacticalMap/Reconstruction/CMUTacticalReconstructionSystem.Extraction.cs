using System.Numerics;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Foldable;
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
    [Dependency] private EntityLookupSystem _lookup = default!;
    private readonly HashSet<EntityUid> _staticCandidates = new();
    private readonly record struct Cell(byte Material, uint Appearance, byte Direction);

    /// <summary>Initial structures, scenery and opted-in furniture, with no inventory or actor state.</summary>
    public byte ReadCell(EntityUid? map, Vector2i tile) => ReadDetails(map, tile, null).Material;

    private Cell ReadDetails(EntityUid? map, Vector2i tile, Atlas? atlas, MapGridComponent? grid = null)
    {
        if (map is not { } uid || grid == null && (TerminatingOrDeleted(uid) || !TryComp(uid, out grid)))
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
        void Consider(EntityUid entity)
        {
            if (TerminatingOrDeleted(entity))
                return;
            if (HasComp<AreaComponent>(entity) || HasComp<AreaLabelComponent>(entity) || HasComp<RoofingEntityComponent>(entity))
                return;
            var prototype = MetaData(entity).EntityPrototype?.ID;
            if (prototype == null)
                return;
            if (TryComp<FoldableComponent>(entity, out var folded) && folded.IsFolded)
                return;
            var kind = TryComp<CMUReconFurnitureComponent>(entity, out var furniture) ? furniture.Material : Kind(prototype);
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
                return;
            priority = score;
            material = kind;
            var rotation = (int) Math.Round(Transform(entity).LocalRotation.Theta / (Math.PI / 2));
            direction = (byte) ((direction & 28) | (rotation & 3));
            if (kind == CMUReconMaterial.Tree && prototype.Contains("Large", StringComparison.OrdinalIgnoreCase))
                direction |= 128; // Larger canopy; lower bits retain facing and floor rotation.
            if (atlas != null)
                detail = Surface(atlas, prototype, door == null ? (byte) 0 : (byte) ((rotation & 3) | (door.State == DoorState.Open ? 4 : 0)), true);
        }
        var anchored = _maps.GetAnchoredEntities(uid, grid, tile);
        while (anchored.MoveNext(out var candidate))
            if (candidate is { } entity) Consider(entity);
        if (atlas != null)
        {
            if (atlas.WorkScenery.TryGetValue(tile, out var scenery))
                foreach (var entity in scenery) Consider(entity);
        }
        else
        {
            FindStaticScenery(uid, tile, 1);
            foreach (var entity in _staticCandidates)
                if (StaticSceneryAt(entity, uid, tile, 1, out _)) Consider(entity);
        }
        return new Cell((byte) material, (uint) surface | ((uint) detail << 16), direction);
    }

    private void FindStaticScenery(EntityUid map, Vector2i origin, int size)
    {
        _staticCandidates.Clear();
        var bounds = new Box2((Vector2) origin, (Vector2) origin + new Vector2(size));
        _lookup.GetEntitiesIntersecting(map, _transform.GetWorldMatrix(map).TransformBox(bounds), _staticCandidates,
            LookupFlags.StaticSundries | LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate);
    }

    private bool StaticSceneryAt(EntityUid entity, EntityUid map, Vector2i origin, int size, out Vector2i tile)
    {
        tile = default;
        if (TerminatingOrDeleted(entity) || !TryComp<TransformComponent>(entity, out var transform) ||
            transform.Anchored || transform.ParentUid != map ||
            MetaData(entity).EntityPrototype is not { } prototype)
            return false;
        // Office/wooden chairs are movable. Capture only explicitly opted-in furniture from
        // dynamic bodies; actors, loose items, vehicles and contained entities remain excluded.
        var isFurniture = TryComp<CMUReconFurnitureComponent>(entity, out var furniture) &&
            CMUReconFurniture.TryGet((byte) furniture.Material, out _);
        var kind = isFurniture ? furniture!.Material : Kind(prototype.ID);
        if (!isFurniture && kind is not (CMUReconMaterial.Tree or CMUReconMaterial.Rock or CMUReconMaterial.Sprite or
                CMUReconMaterial.Furniture or CMUReconMaterial.Crate or CMUReconMaterial.Machinery or CMUReconMaterial.Railing))
            return false;
        if (TryComp<PhysicsComponent>(entity, out var body) && !isFurniture && body.BodyType != Robust.Shared.Physics.BodyType.Static)
            return false;
        if (TryComp<FoldableComponent>(entity, out var folded) && folded.IsFolded)
            return false;
        if (body == null && kind is not (CMUReconMaterial.Tree or CMUReconMaterial.Rock or CMUReconMaterial.Sprite)) return false;
        tile = new Vector2i((int) MathF.Floor(transform.LocalPosition.X), (int) MathF.Floor(transform.LocalPosition.Y));
        return tile.X >= origin.X && tile.Y >= origin.Y && tile.X < origin.X + size && tile.Y < origin.Y + size;
    }

    private void ReadChunkScenery(Atlas atlas, EntityUid map, Vector2i origin)
    {
        atlas.WorkScenery.Clear();
        FindStaticScenery(map, origin, CMUReconGeometry.ChunkSize);
        foreach (var entity in _staticCandidates)
        {
            if (!StaticSceneryAt(entity, map, origin, CMUReconGeometry.ChunkSize, out var tile)) continue;
            if (!atlas.WorkScenery.TryGetValue(tile, out var list)) atlas.WorkScenery[tile] = list = new();
            list.Add(entity);
        }
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
        kind = id.Contains("stump") || id.Contains("log") ? CMUReconMaterial.Sprite :
            id.Contains("tree") ? CMUReconMaterial.Tree :
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

    private static int Priority(CMUReconMaterial material) => CMUReconFurniture.TryGet((byte) material, out _) ? 20 : material switch
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
