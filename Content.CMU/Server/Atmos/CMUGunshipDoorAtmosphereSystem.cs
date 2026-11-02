using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

public sealed partial class CMUGunshipDoorAtmosphereSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
        => SubscribeLocalEvent<CMUGunshipDoorAtmosphereComponent, MapInitEvent>(OnMapInit);

    private void OnMapInit(Entity<CMUGunshipDoorAtmosphereComponent> ent, ref MapInitEvent args)
        => ent.Comp.MapInitialized = true;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUGunshipDoorAtmosphereComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hatch, out var transform))
        {
            if (!hatch.MapInitialized || hatch.Initialized ||
                transform.GridUid is not { } grid ||
                !TryComp(grid, out GridAtmosphereComponent? atmosphere) ||
                !TryComp(grid, out MapGridComponent? gridComp))
                continue;

            // Wait until atmos has processed the complete map, including its doors
            // and fans. A MapInit handler can run before this tile has its volume.
            var indices = _map.CoordinatesToTile(grid, gridComp, transform.Coordinates);
            if (!atmosphere.Tiles.TryGetValue(indices, out var tile))
                continue;

            if (tile.NoGridTile || tile.Air?.Immutable == true)
            {
                hatch.Initialized = true;
                continue;
            }

            if (tile.Air == null)
                continue;

            hatch.Initialized = true;
            if (tile.Air.TotalMoles > Atmospherics.GasMinMoles)
                continue;

            // Closed mapped doors may have no saved gas. Fans isolate them even
            // after opening, so supply their initial breathable air explicitly.
            var air = _atmosphere.GetTileMixture((uid, transform), excite: true);
            if (air == null || air.Immutable)
                continue;

            air.Temperature = Atmospherics.T20C;
            var moles = Atmospherics.OneAtmosphere * air.Volume / (Atmospherics.R * Atmospherics.T20C);
            air.SetMoles(Gas.Oxygen, moles * Atmospherics.OxygenStandard);
            air.SetMoles(Gas.Nitrogen, moles * Atmospherics.NitrogenStandard);
        }
    }
}
