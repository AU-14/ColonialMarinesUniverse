using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Xenonids.Weeds;

public abstract partial class SharedXenoWeedsSystem
{
    // XenoWeedableComponent is access-locked to this system, CMU picks the wall weed through here
    public void CMUSetWeedableSpawn(Entity<XenoWeedableComponent> ent, EntProtoId? spawn)
    {
        if (ent.Comp.Spawn == spawn)
            return;

        ent.Comp.Spawn = spawn;
        Dirty(ent);
    }
}
