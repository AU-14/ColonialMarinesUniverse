using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Mobs;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeCloud;

public sealed partial class CMUSporeCloudOnDeathSystem : EntitySystem
{
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSporeCloudOnDeathComponent, MobStateChangedEvent>(OnPopperDeath);
    }

    private void OnPopperDeath(Entity<CMUSporeCloudOnDeathComponent> xeno, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var cloud = SpawnAtPosition(xeno.Comp.CloudPrototype, Transform(xeno).Coordinates);
        _hive.SetSameHive(xeno.Owner, cloud);
    }
}