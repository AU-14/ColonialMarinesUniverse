using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Ghost.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Uses the same local tile heights for terrain, models, billboards and the predicted camera.</summary>
public sealed partial class CMU3DElevationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    private readonly Dictionary<CMU3DElevationPrototype, CMU3DElevationField> _fields = [];

    public CMU3DElevationField? Field(EntityUid grid)
    {
        if (!TryComp(grid, out CMU3DElevationComponent? elevation) ||
            !_prototypes.TryIndex(elevation.Profile, out var profile))
            return null;
        if (!_fields.TryGetValue(profile, out var field))
            _fields[profile] = field = new CMU3DElevationField(profile);
        return field;
    }

    public float EntityHeight(EntityUid uid, bool stairBase = false)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { } grid ||
            !TryComp(grid, out MapGridComponent? gridComp) || gridComp.TileSize != 1 || Field(grid) is not { } field)
            return 0;
        var local = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(grid));
        if (stairBase && TryComp(uid, out MetaDataComponent? meta) &&
            field.Ramp(local) is { } ramp && meta.EntityPrototype?.ID == ramp.SourcePrototype)
            return ramp.Bottom;
        return field.Height(local);
    }

    public bool SuppressStair(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { } grid ||
            Field(grid) is not { } field || !TryComp(uid, out MetaDataComponent? meta) ||
            meta.EntityPrototype is not { } prototype)
            return false;
        var local = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(grid));
        return field.SuppressedStairs.Contains((new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y)), prototype.ID));
    }

    public int Depth(EntityUid? map) => TryComp(map, out CMUZLevelMapComponent? level) ? level.Depth : 0;

    public float GroundAt(EntityUid map, Vector2 world)
    {
        if (!_map.TryFindGridAt(map, world, out var grid, out var gridComp) || gridComp.TileSize != 1 ||
            Field(grid) is not { } field)
            return 0;
        return field.Height(Vector2.Transform(world, _transform.GetInvWorldMatrix(grid)));
    }

    public float GroundOffset(EntityUid map, int offset, Vector2 world)
    {
        if (offset == 0) return GroundAt(map, world);
        return _zLevels.TryMapOffset(map, offset, out var other)
            ? GroundAt(other.Value.Owner, world) : GroundAt(map, world);
    }

    public float CameraHeight(EntityUid uid, int relativeDepth)
    {
        if (!HasComp<GhostComponent>(uid) || !TryComp(uid, out TransformComponent? xform))
            return PhysicalHeight(uid, relativeDepth);

        // Ghosts retain stair support height because gravity never settles them.
        // Authored flights already describe their full visual ascent on either map.
        var terrain = (Depth(xform.MapUid) - relativeDepth) * CMU3DZProjection.StoryHeight + EntityHeight(uid);
        if (xform.GridUid is { } grid && Field(grid) is { } field)
        {
            var local = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(grid));
            if (field.Ramp(local) != null)
                return terrain;
        }

        // Unfitted stairs still need their physical ascent, but only while supported.
        if (TryComp(uid, out CMUZPhysicsComponent? physics) && physics.LocalPosition != 0)
        {
            _zLevels.DistanceToGround((uid, physics), out var supported);
            if (supported)
                return PhysicalHeight(uid, relativeDepth);
        }
        return terrain;
    }

    public float PhysicalHeight(EntityUid uid, int relativeDepth, bool stairBase = false)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.MapUid is not { } map)
            return 0;
        var depth = Depth(map) - relativeDepth;
        if (TryComp(uid, out CMUZPhysicsComponent? physics) && xform.GridUid is { } grid && Field(grid) is { } field)
        {
            var localPoint = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(grid));
            if (field.Ramp(localPoint) is { PhysicsCurve.Count: > 0 } stair)
            {
                return CMU3DZProjection.SupportedHeight(depth, physics.LocalPosition, stair, localPoint);
            }
        }
        if (physics == null || physics.LocalPosition == 0)
            return depth * CMU3DZProjection.StoryHeight + EntityHeight(uid, stairBase);
        var local = physics.LocalPosition;
        var floor = (int) MathF.Floor(local);
        var world = _transform.GetWorldPosition(xform);
        return CMU3DZProjection.Height(depth, local, GroundOffset(map, floor, world), GroundOffset(map, floor + 1, world));
    }

    public bool TryStair(EntityUid uid, out CMU3DElevationRamp ramp)
    {
        ramp = default!;
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { } grid ||
            !TryComp(grid, out MapGridComponent? gridComp) || gridComp.TileSize != 1 ||
            !xform.Anchored || Field(grid) is not { } field || !TryComp(uid, out MetaDataComponent? meta))
            return false;
        var local = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(grid));
        if (field.Ramp(local) is not { Geometry: true } found || found.SourcePrototype != meta.EntityPrototype?.ID)
            return false;
        ramp = found;
        return true;
    }
}

public sealed class CMU3DElevationField
{
    private readonly Dictionary<Vector2i, float> _heights = [];
    private readonly Dictionary<Vector2i, CMU3DElevationRamp> _ramps = [];

    public readonly HashSet<(Vector2i Tile, string Prototype)> SuppressedStairs = [];
    public readonly HashSet<Vector2i> StairOpenings = [];

    public CMU3DElevationField(CMU3DElevationPrototype profile)
    {
        StairOpenings.UnionWith(profile.StairOpenings);
        foreach (var (prototype, tiles) in profile.SuppressedStairs)
        foreach (var tile in tiles)
            SuppressedStairs.Add((tile, prototype));
        foreach (var region in profile.Regions)
        {
            var bounds = region.Bounds;
            if (!float.IsFinite(region.Height) || MathF.Abs(region.Height) > 16 ||
                !Integer(bounds.Left) || !Integer(bounds.Bottom) || !Integer(bounds.Right) || !Integer(bounds.Top) ||
                bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width * bounds.Height > 100000)
                throw new ArgumentException("Elevation regions require finite heights and bounded tile rectangles.");
            for (var y = (int) bounds.Bottom; y < bounds.Top; y++)
            for (var x = (int) bounds.Left; x < bounds.Right; x++)
                _heights.Add(new Vector2i(x, y), region.Height);
        }
        foreach (var ramp in profile.Ramps)
        {
            if (Math.Abs(ramp.Direction.X) + Math.Abs(ramp.Direction.Y) != 1 ||
                !float.IsFinite(ramp.Bottom) || !float.IsFinite(ramp.Top) ||
                ramp.Top <= ramp.Bottom || ramp.Top - ramp.Bottom > 4)
                throw new ArgumentException("Elevation ramps require a cardinal ascent and positive finite rise.");
            _ramps.Add(ramp.Tile, ramp);
        }
    }

    private static bool Integer(float value) => float.IsFinite(value) && value == MathF.Floor(value);

    public float Floor(Vector2i tile) => _heights.GetValueOrDefault(tile);

    public CMU3DElevationRamp? Ramp(Vector2 local) =>
        _ramps.GetValueOrDefault(new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y)));

    public float Height(Vector2 local)
    {
        var tile = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));
        if (!_ramps.TryGetValue(tile, out var ramp))
            return Floor(tile);
        return ramp.Bottom + Progress(ramp, local) * (ramp.Top - ramp.Bottom);
    }

    public static float Progress(CMU3DElevationRamp ramp, Vector2 local)
    {
        var centered = local - (Vector2) ramp.Tile - new Vector2(.5f);
        return Math.Clamp(Vector2.Dot(centered, (Vector2) ramp.Direction) + .5f, 0, 1);
    }
}
