using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.CMU14.Dropship.MultiDeck;

public sealed class MohawkRampRailingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MohawkRampRailingComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<MohawkRampRailingComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<VehicleComponent>(args.OtherEntity) || HasComp<PowerLoaderComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
