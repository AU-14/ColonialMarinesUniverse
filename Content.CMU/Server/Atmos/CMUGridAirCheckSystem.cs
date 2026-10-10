using Content.Shared.Atmos.Components;
using Robust.Shared.Log;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Reports grids whose GridAtmosphere carries no authored tile air on map init. Unauthored
/// interior tiles are vacuum and every grid a map ships must be baked (fixgridatmos).
/// </summary>
public sealed class CMUGridAirCheckSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logs.GetSawmill("cmu.atmos");

        SubscribeLocalEvent<GridAtmosphereComponent, MapInitEvent>(OnGridAtmosMapInit);
    }

    private void OnGridAtmosMapInit(Entity<GridAtmosphereComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Tiles.Count > 0)
            return;

        if (!TryComp(ent, out MapGridComponent? grid))
            return;

        if (!_map.GetAllTiles(ent.Owner, grid).MoveNext())
            return;

        _sawmill.Warning($"Grid {ToPrettyString(ent)} on map {Transform(ent.Owner).MapID}: " +
                         $"no authored tile air, interiors will be VACUUM. " +
                         $"Fill with fixgridatmos and save before committing (Docs/MAPPERS.md).");
    }
}
