using Content.Shared._RMC14.Damage;
using Content.Shared.Mobs;

namespace Content.Shared.CMU14.Pulling;

public sealed partial class CMUNoDragSuffocationSystem : EntitySystem
{
    public override void Initialize()
    {
        // RMC hands out the active drag damage when the body goes crit, take it straight back off
        SubscribeLocalEvent<CMUNoDragSuffocationComponent, MobStateChangedEvent>(OnMobStateChanged,
            after: [typeof(SharedRMCDamageableSystem)]);
        SubscribeLocalEvent<CMUNoDragSuffocationComponent, ComponentStartup>(OnStartup);
    }

    private void OnMobStateChanged(Entity<CMUNoDragSuffocationComponent> ent, ref MobStateChangedEvent args)
    {
        RemCompDeferred<ActiveDamageOnPulledWhileCritComponent>(ent);
    }

    // a thrall linked while already down
    private void OnStartup(Entity<CMUNoDragSuffocationComponent> ent, ref ComponentStartup args)
    {
        RemCompDeferred<ActiveDamageOnPulledWhileCritComponent>(ent);
    }
}
