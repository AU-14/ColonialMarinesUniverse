using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaSpikeLauncherSystem : EntitySystem
{
    private const string NonYautjaExamineText = "Looks like some kind of...mechanical donut.";

    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, TakeAmmoEvent>(OnTakeAmmo, after: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<YautjaSpikeLauncherProjectileRefundComponent, EntityTerminatingEvent>(OnProjectileTerminating);
    }

    private void OnExamined(Entity<YautjaSpikeLauncherComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<YautjaComponent>(args.Examiner))
        {
            args.PushText(NonYautjaExamineText);
            return;
        }

        if (!TryComp(ent, out BasicEntityAmmoProviderComponent? ammo) ||
            ammo.Count is not { } count ||
            ammo.Capacity is not { } capacity)
        {
            return;
        }

        args.PushMarkup($"It currently has <bold>{count}/{capacity}</bold> spikes.");
    }

    private void OnTakeAmmo(Entity<YautjaSpikeLauncherComponent> ent, ref TakeAmmoEvent args)
    {
        foreach (var (ammoEntity, _) in args.Ammo)
        {
            if (ammoEntity is { } uid)
                AddProjectileRefund(uid, ent.Owner);
        }
    }

    private void OnAmmoShot(Entity<YautjaSpikeLauncherComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out YautjaSpikeLauncherProjectileRefundComponent? refund) ||
                refund.Launcher != ent.Owner)
            {
                continue;
            }

            refund.Fired = true;
        }
    }

    private void OnProjectileTerminating(Entity<YautjaSpikeLauncherProjectileRefundComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Fired ||
            TerminatingOrDeleted(ent.Comp.Launcher) ||
            !HasComp<YautjaSpikeLauncherComponent>(ent.Comp.Launcher) ||
            !TryComp(ent.Comp.Launcher, out BasicEntityAmmoProviderComponent? ammo))
        {
            return;
        }

        _gun.ChangeBasicEntityAmmoCount(ent.Comp.Launcher, 1, ammo);
    }

    private void AddProjectileRefund(EntityUid projectile, EntityUid launcher)
    {
        var refund = EnsureComp<YautjaSpikeLauncherProjectileRefundComponent>(projectile);
        refund.Launcher = launcher;
        refund.Fired = false;
    }
}
