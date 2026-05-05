using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Damage;

namespace Content.Shared._RMC14.Vehicle;

public sealed class VehicleInteriorProtectionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleInteriorIndestructibleComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<VehicleInteriorIndestructibleComponent, CorrodingEvent>(OnCorroding);
    }

    private void OnBeforeDamageChanged(Entity<VehicleInteriorIndestructibleComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.AnyPositive())
            args.Cancelled = true;
    }

    private void OnCorroding(Entity<VehicleInteriorIndestructibleComponent> ent, ref CorrodingEvent args)
    {
        QueueDel(args.Acid);
        args.Cancelled = true;
    }
}
