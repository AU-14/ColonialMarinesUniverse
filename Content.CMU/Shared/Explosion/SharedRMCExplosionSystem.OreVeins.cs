using Content.Shared.Destructible;
using Content.Shared.Mining.Components;

namespace Content.Shared._RMC14.Explosion;

public abstract partial class SharedRMCExplosionSystem
{
    [Dependency] private SharedDestructibleSystem _destructible = default!;

    private void DeleteExplosionTarget(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;

        // Ore is released by DestructionEventArgs, which direct wall deletion bypasses.
        if (HasComp<OreVeinComponent>(uid))
            _destructible.DestroyEntity(uid);
        else
            QueueDel(uid);
    }
}
