using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Dropship.TacticalLand;

public sealed partial class DropshipTacticalLandSystem
{
    private void UpdateHoverJetThrust(Entity<DropshipTacticalHoverComponent> hover)
    {
        if (!TryComp<MapGridComponent>(hover, out var grid))
            return;

        foreach (var uid in hover.Comp.Downwashes)
        {
            if (!TryComp<DropshipTacticalHoverDownwashComponent>(uid, out var wash) || !wash.JetExhaust)
                continue;

            var thrust = GunshipHoverExhaust.NozzleThrust(hover.Comp.GunshipVisualThrust,
                hover.Comp.GunshipVisualTurn, wash.Offset - grid.LocalAABB.Center);
            if (Vector2.DistanceSquared(thrust, wash.ManeuverThrust) < 0.0001f)
                continue;

            wash.ManeuverThrust = thrust;
            Dirty(uid, wash);
        }
    }

    private void CaptureHoverSilhouette(EntityUid dropship, DropshipTacticalHoverShadowComponent shadow)
    {
        if (!TryComp<MapGridComponent>(dropship, out var grid))
            return;

        // Preserve the actual, often off-center origin. Empty support tiles use transparent
        // art, so their alpha must be retained rather than filling the grid's bounding box.
        shadow.HullBounds = grid.LocalAABB.Enlarged(2f);
        foreach (var tile in _map.GetAllTiles(dropship, grid))
        {
            if (_tile[tile.Tile.TypeId] is not ContentTileDefinition { Sprite: { } texture })
                continue;

            shadow.Tiles.Add(new DropshipShadowTile(
                _map.TileCenterToVector(dropship, grid, tile.GridIndices),
                texture.ToString(),
                tile.Tile.Variant));
        }

        var children = Transform(dropship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            var xform = Transform(child);
            if (!xform.Anchored || MetaData(child).EntityPrototype is not { } prototype)
                continue;

            shadow.Parts.Add(new DropshipShadowPart(prototype.ID, xform.LocalPosition, xform.LocalRotation));
        }
    }
}
