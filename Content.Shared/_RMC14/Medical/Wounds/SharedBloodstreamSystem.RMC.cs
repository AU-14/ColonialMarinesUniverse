using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Damage;

namespace Content.Shared.Body.Systems;

public abstract partial class SharedBloodstreamSystem
{
    private bool CanApplyRMCGenericBleeding(Entity<BloodstreamComponent> entity, DamageChangedEvent damage)
    {
        var ev = new CMBleedEvent(damage);
        RaiseLocalEvent(entity, ref ev);
        return !ev.Handled;
    }
}
