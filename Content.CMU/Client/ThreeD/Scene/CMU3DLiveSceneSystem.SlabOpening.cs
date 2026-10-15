using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private readonly List<(int Index, EntityUid Grid, Vector2i Tile, bool Ceiling)> _slabTiles = [];
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile, Box2? Floor, Box2? Ceiling, bool PreserveCladding)> _slabPlans = [];
    private readonly HashSet<EntityUid> _slabAdmitted = [];
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile, Vector2 Center, float GridYaw)> _slabCladdings = [];

    private static bool HasSlabOpening(CMU3DModelPrototype model) => model.FloorOpening != null || model.CeilingOpening != null;

    private bool TrySlabPlan(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model, float entityYaw)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid) || grid.TileSize != 1 ||
            model.GroundOffset != Vector2.Zero || model.Placement != "floor" ||
            model.SpriteStates.Count != 1 || model.SourceDirections != 1)
            return false;
        var gridUid = xform.GridUid!.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var gridYaw = (float) _transform.GetWorldRotation(gridUid).Theta;
        var delta = _transform.GetWorldPosition(xform) - _map.GridTileToWorldPos(gridUid, grid, tile);
        var c = MathF.Cos(gridYaw);
        var s = MathF.Sin(gridYaw);
        var offset = new Vector2(c * delta.X + s * delta.Y, -s * delta.X + c * delta.Y);
        var yaw = CMU3DSceneLayout.RenderYaw(model, entityYaw, sprite.NoRotation, sprite.SnapCardinals) +
                  (float) sprite.Rotation.Theta;
        Box2? floor = null;
        Box2? ceiling = null;
        if (model.FloorOpening != null)
        {
            if (!CMU3DSlabOpening.TryTransform(model.FloorOpening, offset, yaw, gridYaw, out var opening))
                return false;
            floor = opening;
        }
        if (model.CeilingOpening != null)
        {
            if (!CMU3DSlabOpening.TryTransform(model.CeilingOpening, offset, yaw, gridYaw, out var opening))
                return false;
            ceiling = opening;
        }
        _slabPlans[uid] = (gridUid, tile, floor, ceiling, model.PreserveSlabCladding);
        return true;
    }

    private void AddSlab(CMU3DSceneBox box, EntityUid grid, Vector2i tile, bool ceiling)
    {
        _slabTiles.Add((_boxes.Count, grid, tile, ceiling));
        _boxes.Add(box);
    }

    private void CollectSlabCladding(EntityUid uid, string prototype, Vector2 position)
    {
        if (prototype is not ("CMCatwalk" or "CMCatwalkPrison" or "RMCCatwalkHybrisaElevator") ||
            !TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid) || grid.TileSize != 1)
            return;
        var gridUid = xform.GridUid!.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (Vector2.DistanceSquared(_transform.GetWorldPosition(xform), _map.GridTileToWorldPos(gridUid, grid, tile)) > .00000001f)
            return;
        _slabCladdings[uid] = (gridUid, tile, position, (float) _transform.GetWorldRotation(gridUid).Theta);
    }

    /// <summary>Cut only for admitted models. Stage the whole change so a budget failure cannot leave orphan holes.</summary>
    private void ApplySlabOpenings(ref int exact, ref int fallback)
    {
        if (_slabAdmitted.Count == 0)
            return;
        var replacements = new Dictionary<int, List<CMU3DSceneBox>>();
        var count = _boxes.Count;
        var valid = true;
        foreach (var (index, grid, tile, ceiling) in _slabTiles)
        {
            List<Box2> openings = [];
            foreach (var uid in _slabAdmitted)
            {
                var plan = _slabPlans[uid];
                if (plan.Grid != grid || plan.Tile != tile)
                    continue;
                if ((ceiling ? plan.Ceiling : plan.Floor) is { } opening)
                    openings.Add(opening);
            }
            if (openings.Count == 0)
                continue;
            var box = _boxes[index];
            if (!CMU3DSlabOpening.TrySubtract(new Box2(-.5f, -.5f, .5f, .5f), openings, out var fragments))
            {
                valid = false;
                break;
            }
            List<CMU3DSceneBox> boxes = [];
            var c = MathF.Cos(box.Yaw);
            var s = MathF.Sin(box.Yaw);
            foreach (var fragment in fragments)
            {
                var center = fragment.Center;
                boxes.Add(box with
                {
                    Center = box.Center + new Vector3(c * center.X - s * center.Y, s * center.X + c * center.Y, 0),
                    HalfSize = new Vector3(fragment.Width / 2, fragment.Height / 2, box.HalfSize.Z),
                });
            }
            replacements[index] = boxes;
            count += boxes.Count - 1;
        }
        var claddingCounts = new Dictionary<EntityUid, int>();
        for (var index = 0; valid && index < _boxes.Count; index++)
        {
            var box = _boxes[index];
            if (box.Source is not { } source || !_slabCladdings.TryGetValue(source, out var cladding))
                continue;
            List<Box2> openings = [];
            foreach (var uid in _slabAdmitted)
            {
                var plan = _slabPlans[uid];
                if (!plan.PreserveCladding && plan.Grid == cladding.Grid && plan.Tile == cladding.Tile && plan.Floor is { } opening)
                    openings.Add(opening);
            }
            if (openings.Count == 0)
                continue;
            if (!CMU3DSlabCladding.TryClip(box, cladding.Center, cladding.GridYaw, openings, out var fragments))
            {
                valid = false;
                break;
            }
            replacements[index] = fragments;
            count += fragments.Count - 1;
            claddingCounts.TryGetValue(source, out var claddingCount);
            claddingCount += fragments.Count;
            claddingCounts[source] = claddingCount;
            if (claddingCount > CMU3DSlabOpening.MaxFragments)
                valid = false;
        }
        if (!valid || count > ViewPartLimit)
        {
            // Preserve the complete original floor/ceiling and show the original source sprites.
            _boxes.RemoveAll(box => box.Source is { } uid && _slabAdmitted.Contains(uid));
            foreach (var uid in _slabAdmitted)
            {
                _animatedSprites.Remove(uid);
                _fallbackSprites.Add(uid);
            }
            exact -= _slabAdmitted.Count;
            fallback += _slabAdmitted.Count;
            return;
        }
        // Descending indices keep the recorded source slab positions valid.
        for (var index = _boxes.Count - 1; index >= 0; index--)
        {
            if (!replacements.TryGetValue(index, out var boxes))
                continue;
            _boxes.RemoveAt(index);
            _boxes.InsertRange(index, boxes);
        }
    }
}
