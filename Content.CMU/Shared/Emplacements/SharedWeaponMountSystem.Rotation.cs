namespace Content.Shared._RMC14.Emplacements;

public abstract partial class SharedWeaponMountSystem
{
    public void SetRotationLocked(Entity<WeaponMountComponent> ent, bool locked)
    {
        if (ent.Comp.RotationLocked == locked)
            return;

        ent.Comp.RotationLocked = locked;
        DirtyField(ent, ent.Comp, nameof(WeaponMountComponent.RotationLocked));
    }
}
