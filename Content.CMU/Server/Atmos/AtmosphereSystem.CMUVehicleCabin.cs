using Content.Shared._RMC14.Vehicle;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private void InitializeCMUVehicleCabins()
    {
        SubscribeLocalEvent<VehicleInteriorLoadedEvent>(OnCMUVehicleCabinLoaded);
    }

    private void OnCMUVehicleCabinLoaded(ref VehicleInteriorLoadedEvent args)
    {
        if (!TryComp<MapAtmosphereComponent>(args.Grid, out var template) || template.Space
            || !TryComp<GridAtmosphereComponent>(args.Grid, out var atmosphere) || atmosphere.Tiles.Count > 0
            || !TryComp<MapGridComponent>(args.Grid, out var grid))
        {
            return;
        }

        // Older cabin maps store an atmosphere template on their grid. Only the map's atmosphere is used
        // by normal tile initialization, so apply that template to unbaked cabin tiles once at load.
        // Baked air, including intentionally authored vacuum, remains authoritative.
        RebuildGridTiles((args.Grid, atmosphere, EnsureComp<GasTileOverlayComponent>(args.Grid), grid, Transform(args.Grid)));
        foreach (var tile in atmosphere.Tiles.Values)
        {
            if (tile.Air is not { Immutable: false } air)
                continue;

            air.CopyFrom(template.Mixture);
        }
    }
}
