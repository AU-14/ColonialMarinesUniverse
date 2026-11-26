using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Round.Antags.Cannibal;

namespace Content.Server.CMU14.Round.Antags.Cannibal;

public sealed partial class CannibalSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CannibalComponent, ComponentStartup>(OnCannibalSpawned);
    }

    private void OnCannibalSpawned(EntityUid uid, CannibalComponent component, ComponentStartup args)
    {
        EnsureComp<YautjaButcherAbilityComponent>(uid);
    }
}