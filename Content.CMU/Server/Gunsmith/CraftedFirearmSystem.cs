// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Maths;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Gunsmith;

/// <summary>
/// What happens to a weapon made at the bench once it is used: its shots do the damage its design works out to, it heats and wears, and if the
/// player's calculation was wrong the weapon fails: the barrel or the bolt bursts, the stock snaps, the mechanism jams. All of it follows from
/// the same numbers the player was given; nothing here is random in how strong a part is, only in when a mechanism jams.
/// </summary>
public sealed class CraftedFirearmSystem : EntitySystem
{
    /// <summary>The time it takes the barrel to lose half of its excess heat, seconds.</summary>
    public const double CoolingHalfLifeSeconds = GunsmithConstants.CoolingHalfLifeSeconds;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private GunsmithCatalogSystem _catalog = default!;

    private static readonly SoundSpecifier BurstSound = new SoundPathSpecifier("/Audio/Effects/explosion1.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CraftedFirearmComponent, GetFireModesEvent>(OnGetFireModes);
        SubscribeLocalEvent<CraftedFirearmComponent, GunGetFireRateEvent>(OnGetFireRate);
        SubscribeLocalEvent<CraftedFirearmComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<CraftedFirearmComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<CraftedFirearmComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<CraftedFirearmComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<CraftedFirearmComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CraftedFirearmComponent, ContainerIsInsertingAttemptEvent>(OnGunInsertAttempt);

        // A cartridge of another caliber is turned away before the gun starts to load it (the gun of the game puts a cartridge into its list before the container
        // is asked, and a refusal after that would leave a cartridge on the list that is not there).
        SubscribeLocalEvent<CraftedFirearmComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(Content.Server.Weapons.Ranged.Systems.GunSystem) });
        SubscribeLocalEvent<CraftedMagazineComponent, ContainerIsInsertingAttemptEvent>(OnMagazineInsertAttempt);
        SubscribeLocalEvent<CraftedMagazineComponent, InteractUsingEvent>(OnMagazineInteractUsing, before: new[] { typeof(Content.Server.Weapons.Ranged.Systems.GunSystem) });
        SubscribeLocalEvent<CraftedMagazineComponent, ExaminedEvent>(OnMagazineExamined);
    }

    // ------------------------------------------------------------------ the stats of the design

    private void OnGetFireModes(Entity<CraftedFirearmComponent> ent, ref GetFireModesEvent args)
    {
        var modes = SelectiveFire.SemiAuto;
        if ((ent.Comp.Modes & 2) != 0)
            modes |= SelectiveFire.Burst;
        if ((ent.Comp.Modes & 4) != 0)
            modes |= SelectiveFire.FullAuto;

        args.Modes = modes;
    }

    private void OnGetFireRate(Entity<CraftedFirearmComponent> ent, ref GunGetFireRateEvent args)
    {
        // The template gives 1 shot a second in semi-automatic mode (and its multiple in burst); the design scales it.
        args.FireRate *= (float) ent.Comp.FireRate;
    }

    private void OnRefreshModifiers(Entity<CraftedFirearmComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var wielded = TryComp(ent, out WieldableComponent? wieldable) && wieldable.Wielded;
        var scatter = Angle.FromDegrees(ent.Comp.ScatterDegrees * (wielded ? 1.0 : 2.0));

        args.MinAngle = scatter;
        args.MaxAngle = scatter > args.MaxAngle ? scatter : args.MaxAngle;
        args.CameraRecoilScalar = (float) ent.Comp.RecoilFactor * (wielded ? 1f : 3f);
        args.ProjectileSpeed = (float) Math.Clamp(25.0 + ent.Comp.MuzzleVelocity / 20.0, 30.0, 90.0);
    }

    // ------------------------------------------------------------------ shooting

    private void OnShotAttempted(Entity<CraftedFirearmComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-broken"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
            return;
        }

        // The mechanism jams now and then: the worse it is made, and the less the spring of the magazine can push, the oftener.
        var chance = (1.0 - ent.Comp.Reliability) * 0.25;
        if (_container.TryGetContainer(ent, "gun_magazine", out var slot) && slot.ContainedEntities.Count > 0
            && TryComp<CraftedMagazineComponent>(slot.ContainedEntities[0], out var magazine))
        {
            chance += (1.0 - magazine.ReliableShare) * 0.5 + (1.0 - magazine.Reliability) * 0.1;
        }

        // A casing of a poor material is no help either.
        if (_container.TryGetContainer(ent, "gun_chamber", out var chamber) && chamber.ContainedEntities.Count > 0
            && TryComp<CraftedCartridgeComponent>(chamber.ContainedEntities[0], out var chambered))
        {
            chance += chambered.Jam;
        }

        if (chance > 0 && _random.Prob((float) Math.Min(chance, 0.9)))
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-jam"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    /// <summary>
    /// The bullets do the damage the energy of the shot comes to: the load of the cartridge in the barrel of this weapon, by the law of the damage,
    /// times what the materials of the bullet do, plus what the liquid in it does by itself.
    /// </summary>
    private void OnAmmoShot(Entity<CraftedFirearmComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectile, out var component))
                continue;

            // A cartridge of the game does what it does by itself; only what was made at the bench follows the design of the weapon.
            if (!HasComp<CraftedProjectileComponent>(projectile))
                continue;

            var damage = new DamageSpecifier();
            var piercing = ent.Comp.Damage;

            if (TryComp<CraftedProjectileComponent>(projectile, out var marker) && marker.From is { } from
                && _prototypes.TryIndex<GunsmithPowderPrototype>(from.Comp.Load.Powder, out var powder))
            {
                var energy = AmmoMechanics.EnergyIn(from.Comp.Load, powder, ent.Comp.Design.BarrelLengthMm, from.Comp.Seal, out _);
                piercing = from.Comp.GameDamage > 0
                    ? AmmoMechanics.ScaledDamage(from.Comp.GameDamage, from.Comp.GameMaxDamage, energy, from.Comp.BaselineEnergyJ, from.Comp.Pellets, from.Comp.NominalPellets, from.Comp.DamageMultiplier)
                    : AmmoMechanics.DamageOfEnergy(energy / Math.Max(1, from.Comp.Pellets)) * from.Comp.DamageMultiplier;

                foreach (var (type, share) in from.Comp.Extra)
                    AddDamage(damage, type, piercing * share);

                if (from.Comp.PayloadDamageType != null && from.Comp.PayloadDamagePerUnit > 0)
                    AddDamage(damage, from.Comp.PayloadDamageType, from.Comp.PayloadDamagePerUnit * from.Comp.PayloadUnits / Math.Max(1, from.Comp.Pellets));
            }

            AddDamage(damage, "Piercing", piercing);
            component.Damage = damage;
        }
    }

    private static void AddDamage(DamageSpecifier damage, string type, double amount)
    {
        if (amount <= 0)
            return;

        damage.DamageDict[type] = damage.DamageDict.GetValueOrDefault(type) + FixedPoint2.New((float) amount);
    }

    /// <summary>
    /// How much the load that was fired presses compared with the load the weapon was designed for: everything in the weapon is stressed in
    /// proportion to the pressure, so a hot load takes away the safety of the design by this much.
    /// </summary>
    private double PressureRatio(Entity<CraftedFirearmComponent> ent, List<(EntityUid? Uid, IShootable Shootable)> ammo)
    {
        var ratio = 1.0;
        foreach (var (uid, _) in ammo)
        {
            if (uid is not { } cartridge || !TryComp<CraftedCartridgeComponent>(cartridge, out var crafted)
                || !_prototypes.TryIndex<GunsmithPowderPrototype>(crafted.Load.Powder, out var powder))
            {
                continue;
            }

            ratio = AmmoMechanics.PressureOf(crafted.Load, powder) / Math.Max(1.0, ent.Comp.PressureMpa);
            break;
        }

        return ratio;
    }

    private void OnGunShot(Entity<CraftedFirearmComponent> ent, ref GunShotEvent args)
    {
        var comp = ent.Comp;
        var now = _timing.CurTime;

        // The barrel has cooled a little since the last shot (Newton: the excess of the heat halves every CoolingHalfLifeSeconds).
        var gone = Math.Max(0.0, (now - comp.TemperatureAt).TotalSeconds);
        comp.TemperatureC = 20.0 + (comp.TemperatureC - 20.0) * Math.Pow(0.5, gone / CoolingHalfLifeSeconds);
        comp.TemperatureAt = now;

        // The load that was fired: a hotter one than the weapon was made for loads it more, a weaker one less.
        var ratio = PressureRatio(ent, args.Ammo);
        var chargeRatio = 1.0;
        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is { } fired && TryComp<CraftedCartridgeComponent>(fired, out var load))
            {
                chargeRatio = load.Load.ChargeG / Math.Max(0.01, comp.Design.Caliber.ChargeG);
                break;
            }
        }

        comp.TemperatureC += comp.HeatPerShotK * chargeRatio;
        comp.Shots += Math.Pow(ratio, 4.0);

        // 1. The pressure is more than the part holds at all (the design was wrong, or the load too hot): it bursts at the very first shot.
        if (comp.WeakestSafety / ratio < 1.0)
        {
            Burst(ent, args.User, "gunsmith-burst-weak");
            return;
        }

        // 2. The barrel has got hot, and the hot metal is weaker.
        var hotYield = GunsmithMechanics.HotYieldMpa(comp.BarrelYieldMpa, comp.TemperatureC, comp.BarrelLimitTempC);
        if (hotYield < comp.BarrelMisesMpa * ratio)
        {
            Burst(ent, args.User, "gunsmith-burst-hot");
            return;
        }

        // 3. Fatigue: the metal has been worked too hard for too many shots.
        if (comp.Shots > comp.FatigueLifeShots)
        {
            Burst(ent, args.User, "gunsmith-burst-fatigue");
            return;
        }

        // 4. The stock cannot take the kick: it snaps at the first shot and the weapon is of no more use.
        if (!comp.Broken && (comp.StockSafety / ratio < 1.0 || comp.HingeSafety / ratio < 1.0))
        {
            comp.Broken = true;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("gunsmith-stock-snapped"), ent, args.User, PopupType.MediumCaution);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/chopstickbreak.ogg"), ent);
        }
    }

    private void Burst(Entity<CraftedFirearmComponent> ent, EntityUid user, string reason)
    {
        _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.LargeCaution);
        _audio.PlayPvs(BurstSound, ent);
        Spawn("EffectSparks", Transform(ent).Coordinates);

        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = FixedPoint2.New(18);
        damage.DamageDict["Heat"] = FixedPoint2.New(14);
        damage.DamageDict["Blunt"] = FixedPoint2.New(10);
        _damage.TryChangeDamage(user, damage, origin: ent);

        QueueDel(ent);
    }

    // ------------------------------------------------------------------ what goes in

    private void OnInteractUsing(Entity<CraftedFirearmComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CartridgeAmmoComponent>(args.Used))
            return;

        // Only a weapon that takes cartridges itself (a cylinder, a magazine inside) is asked.
        if (!HasComp<RevolverAmmoProviderComponent>(ent) && !HasComp<BallisticAmmoProviderComponent>(ent))
            return;

        // A cartridge of the game of the same caliber (a plain shell) fits as well as one made at the bench.
        if (_catalog.FitsCaliber(args.Used, ent.Comp.CaliberKey))
            return;

        TryComp<CraftedCartridgeComponent>(args.Used, out var cartridge);
        _popup.PopupEntity(Loc.GetString("gunsmith-mismatch-cartridge-weapon",
            ("cartridge", cartridge?.CaliberKey ?? Loc.GetString("gunsmith-mismatch-none")), ("weapon", ent.Comp.CaliberKey)), ent, args.User, PopupType.SmallCaution);
        args.Handled = true;
    }

    private void OnGunInsertAttempt(Entity<CraftedFirearmComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        // A weapon fed from inside (a cylinder, a magazine inside) takes the cartridges themselves: only those of its caliber.
        if (args.Container.ID is "ballistic-ammo" or "revolver-ammo")
        {
            if (_catalog.FitsCaliber(args.EntityUid, ent.Comp.CaliberKey))
                return;

            TryComp<CraftedCartridgeComponent>(args.EntityUid, out var inserted);
            _popup.PopupEntity(Loc.GetString("gunsmith-mismatch-cartridge-weapon",
                ("cartridge", inserted?.CaliberKey ?? Loc.GetString("gunsmith-mismatch-none")), ("weapon", ent.Comp.CaliberKey)), ent, PopupType.SmallCaution);
            args.Cancel();
            return;
        }

        if (args.Container.ID != "gun_magazine")
            return;

        if (!TryComp<CraftedMagazineComponent>(args.EntityUid, out var magazine) || magazine.CaliberKey != ent.Comp.CaliberKey)
        {
            // Said aloud, so that a refusal is never a silent one.
            _popup.PopupEntity(Loc.GetString("gunsmith-mismatch-magazine",
                ("magazine", magazine?.CaliberKey ?? Loc.GetString("gunsmith-mismatch-none")), ("weapon", ent.Comp.CaliberKey)), ent, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    /// <summary>A cartridge of another caliber is turned away by hand before the magazine starts to take it.</summary>
    private void OnMagazineInteractUsing(Entity<CraftedMagazineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CartridgeAmmoComponent>(args.Used))
            return;

        if (_catalog.FitsCaliber(args.Used, ent.Comp.CaliberKey))
            return;

        TryComp<CraftedCartridgeComponent>(args.Used, out var cartridge);
        _popup.PopupEntity(Loc.GetString("gunsmith-mismatch-cartridge",
            ("cartridge", cartridge?.CaliberKey ?? Loc.GetString("gunsmith-mismatch-none")), ("magazine", ent.Comp.CaliberKey)), ent, args.User, PopupType.SmallCaution);
        args.Handled = true;
    }

    private void OnMagazineInsertAttempt(Entity<CraftedMagazineComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != "ballistic-ammo")
            return;

        if (!_catalog.FitsCaliber(args.EntityUid, ent.Comp.CaliberKey))
        {
            TryComp<CraftedCartridgeComponent>(args.EntityUid, out var cartridge);
            _popup.PopupEntity(Loc.GetString("gunsmith-mismatch-cartridge",
                ("cartridge", cartridge?.CaliberKey ?? Loc.GetString("gunsmith-mismatch-none")), ("magazine", ent.Comp.CaliberKey)), ent, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnMagazineExamined(Entity<CraftedMagazineComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("gunsmith-magazine-examine", ("key", ent.Comp.CaliberKey)));
    }

    private void OnExamined(Entity<CraftedFirearmComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gunsmith-examine", ("key", ent.Comp.CaliberKey), ("shots", (int) ent.Comp.Shots)));
        if (ent.Comp.Broken)
            args.PushMarkup(Loc.GetString("gunsmith-examine-broken"));
    }
}
