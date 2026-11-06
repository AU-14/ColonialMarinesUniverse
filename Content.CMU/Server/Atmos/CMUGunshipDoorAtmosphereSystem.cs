using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

public sealed partial class CMUGunshipDoorAtmosphereSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedMapSystem _map = default!;

    // Saved closed hatches may retain trace gas rather than an exactly empty mixture.
    private const float StartupVacuumPressure = 1f;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUGunshipDoorAtmosphereComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hatch, out var transform))
        {
            // Saved initialized grids can skip MapInit for newly mapped children.
            // The map's readiness is authoritative even when that event was skipped.
            if (hatch.Initialized || transform.MapUid is not { } map || !_map.IsInitialized(map)
                || transform.GridUid is not { } grid
                || !TryComp(grid, out GridAtmosphereComponent? atmosphere)
                || !TryComp(grid, out MapGridComponent? gridComp))
                continue;

            // Wait until atmos has processed the complete map, including its doors
            // and fans. A MapInit handler can run before this tile has its volume.
            var indices = _map.CoordinatesToTile(grid, gridComp, transform.Coordinates);
            if (!_atmosphere.HasTileAtmosphere((grid, atmosphere), indices))
                continue;

            var air = _atmosphere.GetTileMixture((uid, transform));
            if (_map.GetTileRef(grid, gridComp, indices).Tile.IsEmpty || air?.Immutable == true)
            {
                hatch.Initialized = true;
                continue;
            }

            if (air == null)
                continue;

            hatch.Initialized = true;
            if (air.Pressure >= StartupVacuumPressure)
                continue;

            // Closed mapped doors may have no saved gas. Fans isolate them even
            // after opening, so supply their initial breathable air explicitly.
            _atmosphere.GetTileMixture((uid, transform), excite: true);

            air.Clear();
            air.Temperature = Atmospherics.T20C;
            var moles = Atmospherics.OneAtmosphere * air.Volume / (Atmospherics.R * Atmospherics.T20C);
            air.SetMoles(Gas.Oxygen, moles * Atmospherics.OxygenStandard);
            air.SetMoles(Gas.Nitrogen, moles * Atmospherics.NitrogenStandard);
        }
    }
}
