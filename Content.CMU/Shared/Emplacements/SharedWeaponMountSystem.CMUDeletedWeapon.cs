using Robust.Shared.Containers;

namespace Content.Shared._RMC14.Emplacements;

public abstract partial class SharedWeaponMountSystem
{
    private void InitializeDeletedWeaponCleanup()
    {
        SubscribeLocalEvent<WeaponMountComponent, EntRemovedFromContainerMessage>(OnDeletedWeaponRemoved);
    }

    private void OnDeletedWeaponRemoved(Entity<WeaponMountComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!_net.IsServer || TerminatingOrDeleted(ent.Owner)
            || ent.Comp.MountedEntity != args.Entity || !TerminatingOrDeleted(args.Entity))
            return;

        // A deleted contained weapon must not remain in the mount's networked state.
        // Ordinary detachment owns its own transition and does not enter this path.
        ent.Comp.MountedEntity = null;
        DirtyField(ent, ent.Comp, nameof(WeaponMountComponent.MountedEntity));
        _buckle.StrapSetEnabled(ent, false);
        UpdateAppearance(ent);
    }
}
