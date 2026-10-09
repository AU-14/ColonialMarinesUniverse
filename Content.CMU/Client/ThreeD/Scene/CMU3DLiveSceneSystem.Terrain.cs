using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private readonly HashSet<Entity<SpriteComponent>> _terrainSources = [];
    private readonly List<(EntityUid Source, CMU3DModelPrototype Model, CMU3DTerrainVolume Volume)> _terrainCutouts = [];
    private readonly List<CMU3DTerrainVolume> _terrainVolumes = [];
    private readonly HashSet<string> _terrainTargetPrototypes = new(StringComparer.Ordinal);
    private readonly Dictionary<IReadOnlyList<CMU3DModelPart>, (Vector3 Low, Vector3 High)> _terrainBounds = [];

    private void CollectTerrainCutouts(Vector2 origin, Box2 bounds, MapId mapId)
    {
        _terrainCutouts.Clear();
        _terrainSources.Clear();
        _terrainTargetPrototypes.Clear();
        _terrainBounds.Clear();
        if (_catalog is not { TerrainQueryPadding: > 0 } catalog)
            return;
        // A cutter center may lie outside the view while its footprint intersects an included terrain entity.
        var padding = new Vector2(catalog.TerrainQueryPadding);
        _lookup.GetEntitiesIntersecting(mapId, new Box2(bounds.BottomLeft - padding, bounds.TopRight + padding),
            _terrainSources, LookupFlags.Uncontained | LookupFlags.Approximate);
        foreach (var (uid, sprite) in _terrainSources)
        {
            if (TerminatingOrDeleted(uid) || !sprite.Visible || sprite.Color.A <= 0 || sprite.ContainerOccluded || !sprite.AddToTree ||
                sprite.Scale != Vector2.One || sprite.Offset != Vector2.Zero ||
                !TryComp(uid, out TransformComponent? xform) || xform.MapID != mapId ||
                !TryComp(uid, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
                _containers.IsEntityOrParentInContainer(uid, meta, xform) ||
                catalog.Resolve(prototype.ID) is not { Exact: true } match || !CMU3DTerrainCutout.Valid(match.Model))
                continue;
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            var model = match.Model;
            var yaw = CMU3DSceneLayout.RenderYaw(model, (float) rotation.Theta, sprite.NoRotation, sprite.SnapCardinals) +
                      (float) sprite.Rotation.Theta;
            if (CMU3DTerrainCutout.TryWorldBounds(model.TerrainCutoutMin, model.TerrainCutoutMax,
                    position - origin + model.GroundOffset, yaw, out var volume))
            {
                _terrainCutouts.Add((uid, model, volume));
                foreach (var target in model.TerrainCutoutTargets)
                    _terrainTargetPrototypes.Add(target.Id);
            }
        }
        _terrainCutouts.Sort((a, b) => a.Source.CompareTo(b.Source));
    }

    private IReadOnlyList<CMU3DModelPart> FitTerrain(EntityUid uid, string prototype, IReadOnlyList<CMU3DModelPart> parts,
        Vector2 position, float yaw)
    {
        if (!_terrainTargetPrototypes.Contains(prototype) || parts.Count == 0)
            return parts;
        // Hundreds of terrain entities can share the same detailed assembly. Its
        // local bounds are identical within this map sample; only the world pose varies.
        if (!_terrainBounds.TryGetValue(parts, out var localBounds))
        {
            var low = new Vector3(float.PositiveInfinity);
            var high = new Vector3(float.NegativeInfinity);
            foreach (var part in parts)
            {
                part.Bounds(out var min, out var max);
                low = Vector3.Min(low, min);
                high = Vector3.Max(high, max);
            }
            _terrainBounds[parts] = localBounds = (low, high);
        }
        if (!CMU3DTerrainCutout.TryWorldBounds(localBounds.Low, localBounds.High, position, yaw, out var terrainBounds))
            return parts;
        _terrainVolumes.Clear();
        foreach (var (source, cutter, volume) in _terrainCutouts)
        {
            if (source == uid || !CMU3DTerrainCutout.Overlaps(terrainBounds.Min, terrainBounds.Max, volume.Min, volume.Max))
                continue;
            foreach (var target in cutter.TerrainCutoutTargets)
            {
                if (target.Id != prototype)
                    continue;
                if (!_terrainVolumes.Contains(volume))
                    _terrainVolumes.Add(volume);
                break;
            }
        }
        return _terrainVolumes.Count > 0 && CMU3DTerrainCutout.TryClip(parts, position, yaw, _terrainVolumes, out var clipped) ? clipped : parts;
    }
}
