using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.CMU14.Weapons.Ranged;

public sealed partial class CMUAmmoProjectileComponentsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAmmoProjectileComponentsComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<CMUAmmoProjectileComponentsComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (MetaData(projectile).EntityPrototype?.ID is not { } protoId ||
                !ent.Comp.Projectiles.Contains(protoId))
            {
                continue;
            }

            EntityManager.AddComponents(projectile, ent.Comp.Components);
        }
    }
}
