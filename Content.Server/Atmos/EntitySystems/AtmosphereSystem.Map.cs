using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems;

public partial class AtmosphereSystem
{
    private void InitializeMap()
    {
        SubscribeLocalEvent<MapAtmosphereComponent, ComponentInit>(OnMapStartup);
        SubscribeLocalEvent<MapAtmosphereComponent, ComponentRemove>(OnMapRemove);
        SubscribeLocalEvent<MapAtmosphereComponent, ComponentGetState>(OnMapGetState);
        SubscribeLocalEvent<GridAtmosphereComponent, EntParentChangedMessage>(OnGridParentChanged);
    }

    private void OnMapStartup(EntityUid uid, MapAtmosphereComponent component, ComponentInit args)
    {
        SanitizeMapMixture(component.Mixture); // CMU14: yaml writes the raw fields, bypassing setter validation
        component.Mixture.MarkImmutable();
        component.Overlay = _gasTileOverlaySystem.GetOverlayData(component.Mixture);
    }

    private void OnMapRemove(EntityUid uid, MapAtmosphereComponent component, ComponentRemove args)
    {
        if (!TerminatingOrDeleted(uid))
            RefreshAllGridMapAtmospheres(uid);
    }

    private void OnMapGetState(EntityUid uid, MapAtmosphereComponent component, ref ComponentGetState args)
    {
        args.State = new MapAtmosphereComponentState(component.Overlay);
    }

    public void SetMapAtmosphere(EntityUid uid, bool space, GasMixture mixture)
    {
        DebugTools.Assert(HasComp<MapComponent>(uid));
        var component = EnsureComp<MapAtmosphereComponent>(uid);
        SetMapGasMixture(uid, mixture, component, false);
        SetMapSpace(uid, space, component, false);
        RefreshAllGridMapAtmospheres(uid);
    }

    public void SetMapGasMixture(EntityUid uid, GasMixture mixture, MapAtmosphereComponent? component = null, bool updateTiles = true)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!mixture.Immutable)
        {
            mixture = mixture.Clone();
            mixture.MarkImmutable();
        }

        SanitizeMapMixture(mixture); // CMU14: non-finite values must never become the relaxation baseline
        component.Mixture = mixture;
        component.Overlay = _gasTileOverlaySystem.GetOverlayData(component.Mixture);
        Dirty(uid, component);
        if (updateTiles)
            RefreshAllGridMapAtmospheres(uid);
    }

    public void SetMapSpace(EntityUid uid, bool space, MapAtmosphereComponent? component = null, bool updateTiles = true)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Space == space)
            return;

        component.Space = space;

        if (updateTiles)
            RefreshAllGridMapAtmospheres(uid);
    }

    // CMU14 method: map yaml deserializes straight into the public fields, so
    // non-finite or negative values skip the setter validation entirely and
    // would poison the immutable baseline that relaxation lerps toward. Zero
    // the garbage moles, default a broken temperature or volume to room
    // conditions.
    private static void SanitizeMapMixture(GasMixture mixture)
    {
        for (var i = 0; i < mixture.Moles.Length; i++)
        {
            var moles = mixture.Moles[i];
            if (!float.IsFinite(moles) || moles < 0f)
                mixture.Moles[i] = 0f;
        }

        if (!float.IsFinite(mixture.Temperature))
            mixture.Temperature = Atmospherics.T20C;

        if (!float.IsFinite(mixture.Volume) || mixture.Volume <= 0f)
            mixture.Volume = Atmospherics.CellVolume;
    }

    /// <summary>
    /// Forces a refresh of all MapAtmosphere tiles on every grid on a map.
    /// </summary>
    public void RefreshAllGridMapAtmospheres(EntityUid map)
    {
        DebugTools.Assert(HasComp<MapComponent>(map));
        var enumerator = AllEntityQuery<GridAtmosphereComponent, TransformComponent>();
        while (enumerator.MoveNext(out var grid, out var atmos, out var xform))
        {
            if (xform.MapUid == map)
                RefreshMapAtmosphereTiles((grid, atmos));
        }
    }

    /// <summary>
    /// Forces a refresh of all MapAtmosphere tiles on a given grid.
    /// </summary>
    private void RefreshMapAtmosphereTiles(Entity<GridAtmosphereComponent?> grid)
    {
        if (!Resolve(grid.Owner, ref grid.Comp))
            return;

        var atmos = grid.Comp;
        foreach (var tile in atmos.MapTiles)
        {
            RemoveMapAtmos(atmos, tile);
            atmos.InvalidatedCoords.Add(tile.GridIndices);
        }
        atmos.MapTiles.Clear();
    }

    /// <summary>
    /// Handles updating map-atmospheres when grids move across maps.
    /// </summary>
    private void OnGridParentChanged(Entity<GridAtmosphereComponent> grid, ref EntParentChangedMessage args)
    {
        // Do nothing if detaching to nullspace
        if (!args.Transform.ParentUid.IsValid())
            return;

        // Avoid doing work if moving from a space-map to another space-map.
        if (args.OldParent == null
            || HasComp<MapAtmosphereComponent>(args.OldParent)
            || HasComp<MapAtmosphereComponent>(args.Transform.ParentUid))
        {
            RefreshMapAtmosphereTiles((grid, grid));
        }
    }
}
