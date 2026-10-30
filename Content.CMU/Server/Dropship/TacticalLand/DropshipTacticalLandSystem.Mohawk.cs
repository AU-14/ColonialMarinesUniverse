using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Dropship.TacticalLand;

public sealed partial class DropshipTacticalLandSystem
{
    private bool TrySpawnMohawkApproachShadows(EntityUid destination, EntityUid ship, float lifetime, TimeSpan touchdown)
    {
        if (!TryComp<MohawkMechanismsComponent>(ship, out var mechanisms) ||
            mechanisms.LandingShadow is not { } prototype || !TryComp<MapGridComponent>(ship, out var grid))
            return false;

        var coordinates = _multiDeck.GetLandingOrigin(ship, Transform(destination).Coordinates, destination);
        var rotation = _transform.GetWorldRotation(destination);
        var roofBounds = new Box2();
        var roofTiles = new List<DropshipShadowTile>();
        if (TryComp<MultiDeckDropshipComponent>(ship, out var assembly) &&
            assembly.Decks.TryGetValue(1, out var roof) && TryComp<MapGridComponent>(roof, out var roofGrid))
        {
            roofBounds = roofGrid.LocalAABB;
            foreach (var tile in _map.GetAllTiles(roof, roofGrid))
            {
                if (_tile[tile.Tile.TypeId] is ContentTileDefinition { Sprite: { } texture })
                {
                    roofTiles.Add(new DropshipShadowTile(_map.TileCenterToVector(roof, roofGrid, tile.GridIndices),
                        texture.ToString(), tile.Tile.Variant));
                }
            }
        }

        AddShadow(coordinates);

        // The destination marks the ground for a normal landing and the cabin's
        // impact floor for a hijack. Both also cast a shadow on the floor above.
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        if (_map.TryGetMap(mapCoordinates.MapId, out var map) && _zLevels.TryMapOffset(map.Value, 1, out var above))
            AddShadow(new EntityCoordinates(above.Value.Owner, mapCoordinates.Position));

        return true;

        void AddShadow(EntityCoordinates position)
        {
            var shadow = Spawn(prototype, position);
            _transform.SetWorldRotation(shadow, rotation);
            var border = EnsureComp<MohawkLandingBorderComponent>(shadow);
            // Mark the occupied hull, including its off-center origin.
            border.Bounds = grid.LocalAABB.Enlarged(0.75f);
            border.ApproachStart = _timing.CurTime;
            border.Touchdown = touchdown;
            border.RoofBounds = roofBounds;
            border.RoofTiles = roofTiles;
            Dirty(shadow, border);
            EnsureComp<TimedDespawnComponent>(shadow).Lifetime = lifetime;
            mechanisms.ApproachShadows.Add(shadow);
        }
    }
}
