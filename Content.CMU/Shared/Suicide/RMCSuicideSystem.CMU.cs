// ReSharper disable CheckNamespace

using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Suicide;

// CMU14: gun suicides consult the chambered ammunition; less-lethal rounds cannot finish one.
public sealed partial class RMCSuicideSystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    // Rubber rounds (Blunt 3), beanbags (0 HP) and taser probes (Piercing 1) sit far below it;
    // the weakest live round in the tree deals at least Piercing 16.
    private const float MinLethalHpDamage = 15f;

    private bool IsLethalAmmo(List<(EntityUid? Entity, IShootable Shootable)> ammo)
    {
        foreach (var (bullet, _) in ammo)
        {
            if (bullet is not { } shot)
                return true;

            // battery and basic providers hand over the spawned projectile
            if (TryComp(shot, out ProjectileComponent? projectile))
            {
                if (HpDamage(projectile.Damage) >= MinLethalHpDamage)
                    return true;
                continue;
            }

            // hitscan shots carry their damage on the shot entity
            if (TryComp(shot, out HitscanBasicDamageComponent? hitscan))
            {
                if (HpDamage(hitscan.Damage) >= MinLethalHpDamage)
                    return true;
                continue;
            }

            // cartridges spawn their projectile on fire, so read it from the prototype
            if (TryComp(shot, out CartridgeAmmoComponent? cartridge)
                && _prototype.TryIndex(cartridge.Prototype, out EntityPrototype? proto)
                && proto.TryComp<ProjectileComponent>(out var cartridgeProjectile, _factory))
            {
                if (HpDamage(cartridgeProjectile.Damage) >= MinLethalHpDamage)
                    return true;
                continue;
            }

            // providers that carry no inspectable damage keep the old behavior
            return true;
        }

        return false;
    }

    private static float HpDamage(DamageSpecifier damage)
    {
        float total = 0;

        foreach (var (type, value) in damage.DamageDict)
        {
            if (type.Id != "Stamina")
                total += (float) value;
        }

        return total;
    }
}
