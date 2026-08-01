using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Evolution;

namespace Content.Shared._CMU14.Hiveless;

public sealed class HivelessSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HivelessComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HivelessComponent> ent, ref MapInitEvent args)
    {
        RemoveHive(ent);
    }

    private void RemoveHive(Entity<HivelessComponent> ent)
    {
        if (!HasComp<HivelessComponent>(ent.Owner))
            return;
        RemComp<HivelessComponent>(ent);
        RemComp<HiveMemberComponent>(ent);
        RemComp<XenoEvolutionComponent>(ent);
        RemComp<XenoDevolveComponent>(ent);
    }
}
