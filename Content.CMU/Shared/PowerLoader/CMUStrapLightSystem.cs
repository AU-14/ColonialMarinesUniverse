using Content.Shared.Buckle.Components;

namespace Content.Shared.CMU14.PowerLoader;

public sealed partial class CMUStrapLightSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUStrapLightComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<CMUStrapLightComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnStrapped(Entity<CMUStrapLightComponent> ent, ref StrappedEvent args)
    {
        _pointLight.SetEnabled(ent, true);
    }

    private void OnUnstrapped(Entity<CMUStrapLightComponent> ent, ref UnstrappedEvent args)
    {
        _pointLight.SetEnabled(ent, args.Strap.Comp.BuckledEntities.Count > 0);
    }
}
