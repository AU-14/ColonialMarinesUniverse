using Content.Shared._RMC14.Xenonids.Weeds;

namespace Content.Shared.CMU14.Xenonids.Weeds;

public sealed partial class CMUWallWeedsSpawnSystem : EntitySystem
{
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    public override void Initialize()
    {
        // raised on the weed source right before the wall weed spawns, so the wall's spawn can still be swapped
        SubscribeLocalEvent<XenoWeedsComponent, AfterEntityWeedingEvent>(OnAfterWeeding);
    }

    private void OnAfterWeeding(Entity<XenoWeedsComponent> ent, ref AfterEntityWeedingEvent args)
    {
        if (!TryComp(args.CoveredEntity, out XenoWeedableComponent? weedable))
            return;

        if (TryComp(args.Weeds, out CMUWallWeedsSpawnComponent? wall))
        {
            _weeds.CMUSetWeedableSpawn((args.CoveredEntity, weedable), wall.Wall);
            return;
        }

        // ordinary weeds put the wall back to its own prototype's wall weed, in case pathogen got there first
        if (MetaData(args.CoveredEntity).EntityPrototype is { } proto &&
            proto.TryGetComponent(out XenoWeedableComponent? original, EntityManager.ComponentFactory))
        {
            _weeds.CMUSetWeedableSpawn((args.CoveredEntity, weedable), original.Spawn);
        }
    }
}
