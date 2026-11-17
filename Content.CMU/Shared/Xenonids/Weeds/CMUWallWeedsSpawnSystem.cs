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

        // always start from the prototype's own wall weed, the live one may still be the other hive's
        if (MetaData(args.CoveredEntity).EntityPrototype is not { } proto ||
            !proto.TryGetComponent(out XenoWeedableComponent? original, EntityManager.ComponentFactory))
        {
            return;
        }

        var spawn = original.Spawn;
        if (spawn is { } own &&
            TryComp(args.Weeds, out CMUWallWeedsSpawnComponent? walls) &&
            walls.Walls.TryGetValue(own, out var swapped))
        {
            spawn = swapped;
        }

        _weeds.CMUSetWeedableSpawn((args.CoveredEntity, weedable), spawn);
    }
}
