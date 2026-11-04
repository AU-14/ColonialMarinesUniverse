using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Nutrition;

/// <summary>
///     Ported from cmss13 PR 7895: a ration is named after the main course it was packed with,
///     e.g. "MRE (boneless pork ribs)".
/// </summary>
[RegisterComponent]
public sealed partial class CMUMREEntreeNameComponent : Component;

/// <summary>
///     Marks an MRE main course packet. The ration takes the name of the food this packet opens into.
/// </summary>
[RegisterComponent]
public sealed partial class CMUMREEntreeComponent : Component;

public sealed class CMUMREEntreeNameSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUMREEntreeNameComponent, MapInitEvent>(OnMapInit, after: [typeof(StorageSystem)]);
    }

    private void OnMapInit(Entity<CMUMREEntreeNameComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out StorageComponent? storage))
            return;

        foreach (var contained in storage.Container.ContainedEntities)
        {
            if (!HasComp<CMUMREEntreeComponent>(contained) ||
                !TryComp(contained, out SpawnItemsOnUseComponent? spawn) ||
                spawn.Items.Count == 0 ||
                spawn.Items[0].PrototypeId is not { } food ||
                !_prototypes.TryIndex(food, out var foodProto))
            {
                continue;
            }

            _metaData.SetEntityName(ent, Loc.GetString("cmu-mre-name-with-entree",
                ("mre", Name(ent)),
                ("entree", foodProto.Name)));
            return;
        }
    }
}
