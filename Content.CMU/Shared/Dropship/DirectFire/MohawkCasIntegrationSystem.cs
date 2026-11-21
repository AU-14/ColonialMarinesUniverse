using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.Cas;

namespace Content.Shared.CMU14.Dropship.DirectFire;

/// <summary>Connects optional CAS fire control and obscurants to the standalone M90 events.</summary>
public sealed class MohawkCasIntegrationSystem : EntitySystem
{
    [Dependency] private DropshipFireControlBootSystem _boot = default!;
    [Dependency] private CMUCasFalloutSystem _fallout = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipWeaponComponent, GunshipWeaponReadyEvent>(OnReady);
        SubscribeLocalEvent<DropshipWeaponPointComponent, GunshipAmmoConsumedEvent>(OnConsumed);
        SubscribeLocalEvent<MohawkChinGunComponent, GunshipTargetVisibilityEvent>(OnVisibility);
    }

    private void OnReady(Entity<DropshipWeaponComponent> ent, ref GunshipWeaponReadyEvent args)
    {
        args.Cancelled |= !_boot.IsWeaponReady(ent);
    }

    private void OnConsumed(Entity<DropshipWeaponPointComponent> ent, ref GunshipAmmoConsumedEvent args)
    {
        var engagement = _boot.RegisterUse(ent);
        // Continuous M90 fire rolls one stoppage per engagement, not per shell.
        if (engagement && TryComp(ent, out MohawkChinGunComponent? chin))
            _boot.TryJam(ent, chin.Operator);
    }

    private void OnVisibility(Entity<MohawkChinGunComponent> ent, ref GunshipTargetVisibilityEvent args)
    {
        args.Obscured |= _fallout.IsObscured(args.Coordinates);
    }
}
