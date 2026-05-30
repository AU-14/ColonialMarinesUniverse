using Robust.Server.GameStates;

namespace Content.Server._CMU14.ZLevels.PVS;

public sealed partial class CMUPvsOverrideSystem : EntitySystem
{
    [Dependency] private PvsOverrideSystem _pvs = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPvsOverrideComponent, ComponentStartup>(OnLighthouseStartup);
        SubscribeLocalEvent<CMUPvsOverrideComponent, ComponentShutdown>(OnLighthouseShutdown);
    }

    private void OnLighthouseShutdown(Entity<CMUPvsOverrideComponent> ent, ref ComponentShutdown args)
    {
        _pvs.RemoveGlobalOverride(ent);
    }

    private void OnLighthouseStartup(Entity<CMUPvsOverrideComponent> ent, ref ComponentStartup args)
    {
        _pvs.AddGlobalOverride(ent);
    }
}
