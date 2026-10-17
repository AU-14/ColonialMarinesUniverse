using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Dropship;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Dropship;

// rmc's BeforeFTLFinished sweep guesses a box centred on the landing origin and ignores rotation,
// so off-centre hulls (lexington's nose) or turned pads left ground fires burning inside the cabin.
// this runs after the ship is actually down and checks the real hull tiles instead.
public sealed partial class CMUDropshipLandingFireSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<TileFireComponent>> _fires = new();

    public override void Initialize()
    {
        // broadcast sub, the per-component FTLCompletedEvent slots are already taken
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        var ship = args.Entity;
        if (!HasComp<DropshipComponent>(ship) ||
            !TryComp(ship, out MapGridComponent? grid) ||
            !TryComp(ship, out TransformComponent? xform) ||
            xform.MapUid == null)
        {
            return;
        }

        ExtinguishUnderHull((ship, grid, xform));
    }

    public void ExtinguishUnderHull(Entity<MapGridComponent, TransformComponent> ship)
    {
        var mapId = ship.Comp2.MapID;
        var worldBounds = _transform.GetWorldMatrix(ship.Comp2).TransformBox(ship.Comp1.LocalAABB);

        _fires.Clear();
        _lookup.GetEntitiesIntersecting(mapId, worldBounds, _fires, LookupFlags.Uncontained);
        foreach (var fire in _fires)
        {
            var fireXform = Transform(fire);

            // fires lit on the ship's own deck are legit, leave them
            if (fireXform.GridUid == ship.Owner)
                continue;

            // the world aabb overhangs the hull, only kill fires actually under a floor tile
            var mapPos = _transform.GetMapCoordinates(fire, fireXform);
            var local = _transform.ToCoordinates((ship.Owner, ship.Comp2), mapPos).Position;
            if (!_map.TryGetTileRef(ship.Owner, ship.Comp1, local.Floored(), out var tile) ||
                tile.Tile.IsEmpty)
            {
                continue;
            }

            QueueDel(fire);
        }
    }
}
