using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Telephone;

namespace Content.Server.CMU14.Telephone;

public sealed partial class CMUPayphoneSystem : EntitySystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPayphoneComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUPayphoneComponent> ent, ref MapInitEvent args)
    {
        if (!_areas.TryGetArea(ent.Owner, out _, out var area))
            return;

        _metaData.SetEntityName(ent, Loc.GetString("cmu-payphone-name", ("area", area.Name)));
    }
}
