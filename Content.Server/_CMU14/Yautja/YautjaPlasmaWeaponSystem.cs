using Content.Server.Power.Components;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Examine;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaPlasmaWeaponSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaPlasmaWeaponComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaPlasmaWeaponComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<YautjaPlasmaWeaponComponent, TakeAmmoEvent>(OnTakeAmmo, after: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<YautjaPlasmaWeaponComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<YautjaPlasmaWeaponComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<YautjaPlasmaWeaponProjectileRefundComponent, EntityTerminatingEvent>(OnProjectileTerminating);
    }

    private void OnExamined(Entity<YautjaPlasmaWeaponComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<YautjaComponent>(args.Examiner))
        {
            if (ent.Comp.NonYautjaExamineText.Length > 0)
                args.PushText(Loc.GetString(ent.Comp.NonYautjaExamineText));

            return;
        }

        if (!TryComp(ent, out BatteryComponent? battery))
        {
            return;
        }

        var charge = (int) MathF.Round(battery.CurrentCharge);
        var maxCharge = (int) MathF.Round(battery.MaxCharge);
        args.PushMarkup(Loc.GetString(
            "cmu-yautja-plasma-weapon-examine-charge",
            ("charge", charge),
            ("max", maxCharge)));

        if (!ent.Comp.ShowFireMode ||
            !TryComp(ent, out BatteryWeaponFireModesComponent? fireModes))
        {
            return;
        }

        var text = fireModes.CurrentFireMode == 1
            ? ent.Comp.SecondaryFireModeText
            : ent.Comp.PrimaryFireModeText;

        if (text.Length > 0)
            args.PushMarkup(Loc.GetString(text));
    }

    private void OnAttemptShoot(Entity<YautjaPlasmaWeaponComponent> ent, ref AttemptShootEvent args)
    {
        if ((args.Cancelled && args.Message != null) ||
            ent.Comp.MinimumShootCharge <= 0 ||
            ent.Comp.LowPowerWarning.Length == 0 ||
            !CanUseYautjaTech(args.User) ||
            !TryComp(ent, out BatteryComponent? battery) ||
            battery.CurrentCharge >= ent.Comp.MinimumShootCharge)
        {
            return;
        }

        args.Cancelled = true;
        args.Message = Loc.GetString(ent.Comp.LowPowerWarning);
    }

    private void OnTakeAmmo(Entity<YautjaPlasmaWeaponComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.Ammo.Count != 0)
        {
            if (ent.Comp.RefundUnfiredProjectiles &&
                TryComp(ent, out ProjectileBatteryAmmoProviderComponent? existingAmmo))
            {
                foreach (var (ammoEntity, _) in args.Ammo)
                {
                    if (ammoEntity is { } uid)
                        AddProjectileRefund(uid, ent.Owner, existingAmmo.FireCost);
                }
            }

            return;
        }

        if (args.Shots <= 0 ||
            ent.Comp.MinimumAmmoCharge <= 0 ||
            args.User is { } user && !CanUseYautjaTech(user) ||
            !TryComp(ent, out ProjectileBatteryAmmoProviderComponent? ammo) ||
            !TryComp(ent, out BatteryComponent? battery) ||
            battery.CurrentCharge < ent.Comp.MinimumAmmoCharge)
        {
            return;
        }

        var projectile = Spawn(ammo.Prototype, args.Coordinates);
        args.Ammo.Add((projectile, EnsureComp<AmmoComponent>(projectile)));

        var charge = new ChangeChargeEvent(-ammo.FireCost);
        RaiseLocalEvent(ent, ref charge);
        var spentCharge = ammo.FireCost + charge.ResidualValue;

        if (ent.Comp.RefundUnfiredProjectiles && spentCharge > 0)
            AddProjectileRefund(projectile, ent.Owner, spentCharge);
    }

    private void OnAmmoShot(Entity<YautjaPlasmaWeaponComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out YautjaPlasmaWeaponProjectileRefundComponent? refund) ||
                refund.Weapon != ent.Owner)
            {
                continue;
            }

            refund.Fired = true;
        }
    }

    private void OnProjectileTerminating(Entity<YautjaPlasmaWeaponProjectileRefundComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Fired ||
            TerminatingOrDeleted(ent.Comp.Weapon) ||
            !HasComp<YautjaPlasmaWeaponComponent>(ent.Comp.Weapon))
        {
            return;
        }

        var charge = new ChangeChargeEvent(ent.Comp.ChargeCost);
        RaiseLocalEvent(ent.Comp.Weapon, ref charge);
    }

    private void AddProjectileRefund(EntityUid projectile, EntityUid weapon, float chargeCost)
    {
        var refund = EnsureComp<YautjaPlasmaWeaponProjectileRefundComponent>(projectile);
        refund.Weapon = weapon;
        refund.ChargeCost = chargeCost;
    }

    private void OnChargeChanged(Entity<YautjaPlasmaWeaponComponent> ent, ref ChargeChangedEvent args)
    {
        if (ent.Comp.MaxChargePopup.Length == 0 ||
            !MathHelper.CloseTo(args.Charge, args.MaxCharge) ||
            !TryGetDirectMobHolder(ent.Owner, out var holder))
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString(ent.Comp.MaxChargePopup), ent.Owner, holder);
    }

    private bool TryGetDirectMobHolder(EntityUid item, out EntityUid holder)
    {
        holder = default;
        if (!_containers.TryGetContainingContainer((item, null, null), out var container) ||
            !HasComp<MobStateComponent>(container.Owner))
        {
            return false;
        }

        holder = container.Owner;
        return true;
    }

    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               HasComp<BypassInteractionChecksComponent>(user);
    }
}
