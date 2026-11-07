using Content.Shared.CMU14.Materials;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Materials;

public sealed class CMUMaterialReclaimerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMaterialReclaimerComponent, CMUReclaimMaterialEvent>(OnReclaim);
    }

    private void OnReclaim(Entity<CMUMaterialReclaimerComponent> ent, ref CMUReclaimMaterialEvent args)
    {
        if (ent.Comp.Conversions.TryGetValue(args.Material, out var converted))
            args.Material = converted;
    }
}
