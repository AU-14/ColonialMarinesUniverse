using System.Numerics;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Explosion.EntitySystems;

// This part of the system handled send visual / overlay data to clients.
public sealed partial class ExplosionSystem
{
    // Published on the simulation thread; state generation only reads these snapshots.
    private readonly Dictionary<EntityUid, (GameTick Tick, ExplosionVisualsState State)> _visualStates = new();

    /// <summary>
    /// Initializes the visual parts of this system.
    /// </summary>
    /// <see cref="Initialize"/>
    public void InitVisuals()
    {
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentRemove>(OnVisualsRemoved);
    }

    private void OnGetState(EntityUid uid, ExplosionVisualsComponent component, ref ComponentGetState args)
    {
        args.State = _visualStates.TryGetValue(uid, out var published) && published.Tick == component.LastModifiedTick
            ? published.State
            : BuildVisualState(component);
    }

    private void OnVisualsRemoved(Entity<ExplosionVisualsComponent> ent, ref ComponentRemove args)
    {
        _visualStates.Remove(ent.Owner);
    }

    /// <summary>
    /// Publish after changing visual data, before parallel state generation. Completed flood lists are read-only;
    /// only the outer grid dictionary changes on grid removal, so each publication owns that dictionary.
    /// </summary>
    public void PublishVisualState(Entity<ExplosionVisualsComponent> ent)
    {
        Dirty(ent);
        _visualStates[ent.Owner] = (ent.Comp.LastModifiedTick, BuildVisualState(ent.Comp));
    }

    private ExplosionVisualsState BuildVisualState(ExplosionVisualsComponent component)
    {
        Dictionary<NetEntity, Dictionary<int, List<Vector2i>>> tileLists = new(component.Tiles.Count);
        foreach (var (grid, data) in component.Tiles)
        {
            if (TryGetNetEntity(grid, out var net) && net != NetEntity.Invalid)
                tileLists.Add(net.Value, data);
        }

        return new ExplosionVisualsState(
            component.Epicenter,
            component.ExplosionType,
            component.Intensity,
            component.SpaceTiles,
            tileLists,
            component.SpaceMatrix,
            component.SpaceTileSize);
    }

    /// <summary>
    ///     Constructor for the shared <see cref="ExplosionEvent"/> using the server-exclusive explosion classes.
    /// </summary>
    private EntityUid CreateExplosionVisualEntity(MapCoordinates epicenter, string prototype, Matrix3x2 spaceMatrix, ExplosionSpaceTileFlood? spaceData, IEnumerable<ExplosionGridTileFlood> gridData, List<float> iterationIntensity)
    {
        var explosionEntity = Spawn(null, MapCoordinates.Nullspace);
        var comp = AddComp<ExplosionVisualsComponent>(explosionEntity);

        foreach (var grid in gridData)
        {
            comp.Tiles.Add(grid.Grid.Owner, grid.TileLists);
        }

        comp.SpaceTiles = spaceData?.TileLists;
        comp.Epicenter = epicenter;
        comp.ExplosionType = prototype;
        comp.Intensity = iterationIntensity;
        comp.SpaceMatrix = spaceMatrix;
        comp.SpaceTileSize = spaceData?.TileSize ?? DefaultTileSize;
        PublishVisualState((explosionEntity, comp));

        // Light, sound & visuals may extend well beyond normal PVS range. In principle, this should probably still be
        // restricted to something like the same map, but whatever.
        _pvsSys.AddGlobalOverride(explosionEntity);

        var appearance = AddComp<AppearanceComponent>(explosionEntity);
        _appearance.SetData(explosionEntity, ExplosionAppearanceData.Progress, 1, appearance);

        return explosionEntity;
    }
}
