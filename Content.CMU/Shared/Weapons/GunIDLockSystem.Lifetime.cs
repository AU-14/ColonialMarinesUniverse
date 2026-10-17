using Content.Shared.CMU14.EntityReferences;

namespace Content.Shared._RMC14.Weapons.Ranged;

public sealed partial class GunIDLockSystem
{
    [Dependency] private EntityReferenceSystem _cmuReferences = default!;

    private void InitializeCMUUserLifetime()
    {
        SubscribeLocalEvent<GunIDLockComponent, ReferencedEntityTerminatingEvent>(OnCMUUserTerminating);
    }

    private void OnCMUUserTerminating(Entity<GunIDLockComponent> gun, ref ReferencedEntityTerminatingEvent args)
    {
        // Deletion already permits re-registration in CheckUserRevivability; clear before replication.
        if (gun.Comp.User == args.Entity)
            ClearUser(gun);
    }
}
