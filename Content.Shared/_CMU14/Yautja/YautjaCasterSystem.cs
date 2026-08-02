using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaCasterSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCasterComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaCasterComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<YautjaCasterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaCasterComponent, GunCooldownAttemptEvent>(OnGunCooldownAttempted);
        SubscribeLocalEvent<YautjaCasterComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<YautjaCasterComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<YautjaCasterComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<YautjaCasterComponent, TakeAmmoEvent>(OnTakeAmmo, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<YautjaCasterComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<YautjaCasterComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<YautjaCasterProjectileRefundComponent, EntityTerminatingEvent>(OnProjectileTerminating);
    }

    private void OnUseInHand(Entity<YautjaCasterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Modes.Count < 2)
            return;

        args.Handled = true;

        if (!CanUseCasterTech(args.User))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var mode = GetStrengthToggleMode(ent.Comp);
        if (mode == null)
            return;

        if (_net.IsClient)
        {
            PopupMode(ent, args.User, "cmu-yautja-caster-mode-next", mode.Value);
            return;
        }

        var cost = (ent.Comp.CurrentMode, mode.Value) switch
        {
            (0, 1) => (FixedPoint2) 150,
            (1, 0) => (FixedPoint2) 30,
            (2, 3) => (FixedPoint2) 1000,
            (3, 2) => (FixedPoint2) 500,
            _ => ent.Comp.PowerCost,
        };

        SetMode(ent, mode.Value, cost);
        PopupMode(ent, args.User, "cmu-yautja-caster-mode-set");
    }

    private void OnUniqueAction(Entity<YautjaCasterComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled || ent.Comp.Modes.Count < 3)
            return;

        args.Handled = true;

        if (!CanUseCasterTech(args.UserUid))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), args.UserUid, args.UserUid, PopupType.SmallCaution);
            return;
        }

        var isLethal = IsLethalMode(ent.Comp);
        var mode = isLethal ? 0 : 2;
        var cost = isLethal ? (FixedPoint2) 30 : 100;

        if (_net.IsClient)
        {
            PopupMode(ent, args.UserUid, "cmu-yautja-caster-mode-next", mode);
            return;
        }

        SetMode(ent, mode, cost);
        PopupMode(ent, args.UserUid, "cmu-yautja-caster-mode-set");
    }

    private void OnExamined(Entity<YautjaCasterComponent> ent, ref ExaminedEvent args)
    {
        var mode = GetMode(ent.Comp);
        if (mode == null)
            return;

        var strength = mode.ExamineStrength.Length > 0
            ? Loc.GetString(mode.ExamineStrength)
            : Loc.GetString(mode.Name);

        args.PushMarkup(Loc.GetString("cmu-yautja-caster-fire-mode", ("mode", strength)));
    }

    private void OnAttemptShoot(Entity<YautjaCasterComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryGetSourceBracer(ent.Owner, out var sourceBracer))
        {
            args.Cancelled = true;
            return;
        }

        if (!CanUseCasterTech(args.User))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("cmu-yautja-spike-launcher-denied");
            return;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.CooldownUntil)
        {
            args.Cancelled = true;
            PopupCooldown(ent, args.User, ent.Comp.CooldownUntil);

            return;
        }

        ApplyMode(ent);
        if (!_power.HasPowerPopup(sourceBracer, args.User, GetPowerCost(ent.Comp)))
        {
            args.Cancelled = true;
        }
    }

    private void OnShotAttempted(Entity<YautjaCasterComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || !CanUseCasterTech(args.User))
            return;

        var cooldownUntil = args.Used.Comp.NextFire;
        if (cooldownUntil <= _timing.CurTime)
            return;

        PopupCooldown(ent, args.User, cooldownUntil);
    }

    private void OnGunCooldownAttempted(Entity<YautjaCasterComponent> ent, ref GunCooldownAttemptEvent args)
    {
        if (!CanUseCasterTech(args.User))
            return;

        PopupCooldown(ent, args.User, args.Used.Comp.NextFire);
    }

    private void PopupCooldown(Entity<YautjaCasterComponent> ent, EntityUid user, TimeSpan cooldownUntil)
    {
        var remaining = (int) Math.Ceiling((cooldownUntil - _timing.CurTime).TotalSeconds);
        _popup.PopupClient(Loc.GetString("cmu-yautja-caster-cooldown", ("seconds", remaining)), ent.Owner, user, PopupType.SmallCaution);

        if (ent.Comp.CooldownSound != null)
            _audio.PlayPredicted(ent.Comp.CooldownSound, ent.Owner, user);
    }

    private void OnTakeAmmo(Entity<YautjaCasterComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.Ammo.Count != 0 || args.Shots <= 0)
            return;

        if (args.User is not { } user ||
            !TryGetSourceBracer(ent.Owner, out var sourceBracer) ||
            GetMode(ent.Comp) is not { } mode)
        {
            args.Reason = Loc.GetString("cmu-yautja-spike-launcher-denied");
            return;
        }

        var chargeCost = GetPowerCost(ent.Comp);
        for (var shot = 0; shot < args.Shots; shot++)
        {
            if (_net.IsServer && !_power.TryDrainPower(sourceBracer, user, chargeCost, popup: false))
            {
                args.Reason = GetPowerFailureMessage(sourceBracer.Comp, chargeCost);
                return;
            }

            var projectile = Spawn(mode.Projectile, args.Coordinates);
            if (_net.IsServer)
                AddProjectileRefund(projectile, sourceBracer, chargeCost);

            args.Ammo.Add((projectile, _gun.EnsureShootable(projectile)));
        }
    }

    private void OnAmmoShot(Entity<YautjaCasterComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out YautjaCasterProjectileRefundComponent? refund))
                continue;

            refund.Fired = true;
        }
    }

    private void OnGunShot(Entity<YautjaCasterComponent> ent, ref GunShotEvent args)
    {
        _audio.PlayPredicted(GetFireSound(ent.Comp), ent.Owner, args.User);
    }

    private void OnProjectileTerminating(Entity<YautjaCasterProjectileRefundComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Fired ||
            TerminatingOrDeleted(ent.Comp.Bracer) ||
            !TryComp(ent.Comp.Bracer, out YautjaBracerComponent? bracer))
        {
            return;
        }

        _power.RegenPower((ent.Comp.Bracer, bracer), ent.Comp.ChargeCost);
    }

    private void SetMode(Entity<YautjaCasterComponent> ent, int mode, FixedPoint2 powerCost)
    {
        ent.Comp.CurrentMode = mode;
        ent.Comp.PowerCost = powerCost;
        Dirty(ent);
        ApplyMode(ent);
    }

    private void ApplyMode(Entity<YautjaCasterComponent> ent)
    {
        var mode = GetMode(ent.Comp);
        if (mode == null)
            return;

        _gun.RefreshModifiers(ent.Owner);
    }

    private static void OnGunRefreshModifiers(Entity<YautjaCasterComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var mode = GetMode(ent.Comp);
        if (mode?.FireRate > 0)
            args.FireRate = mode.FireRate;
    }

    private static YautjaCasterMode? GetMode(YautjaCasterComponent component)
    {
        if (component.Modes.Count == 0)
            return null;

        var mode = component.CurrentMode;
        if (mode < 0 || mode >= component.Modes.Count)
            mode = 0;

        return component.Modes[mode];
    }

    private static FixedPoint2 GetPowerCost(YautjaCasterComponent component)
    {
        return component.PowerCost;
    }

    private static Robust.Shared.Audio.SoundSpecifier GetFireSound(YautjaCasterComponent component)
    {
        return GetMode(component)?.FireSound ?? component.FireSound;
    }

    private static int? GetStrengthToggleMode(YautjaCasterComponent component)
    {
        if (IsLethalMode(component))
        {
            if (component.Modes.Count <= 3)
                return null;

            return component.CurrentMode == 3 ? 2 : 3;
        }

        if (component.Modes.Count <= 1)
            return null;

        return component.CurrentMode == 1 ? 0 : 1;
    }

    private static bool IsLethalMode(YautjaCasterComponent component)
    {
        return component.CurrentMode >= 2;
    }

    private bool CanUseCasterTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user);
    }

    private bool TryGetSourceBracer(EntityUid caster, out Entity<YautjaBracerComponent> bracer)
    {
        bracer = default;
        if (!TryComp(caster, out YautjaStoredGearComponent? stored) ||
            stored.Bracer is not { } source ||
            TerminatingOrDeleted(source) ||
            !TryComp(source, out YautjaBracerComponent? sourceComp))
        {
            return false;
        }

        bracer = (source, sourceComp);
        return true;
    }

    private void AddProjectileRefund(EntityUid projectile, Entity<YautjaBracerComponent> bracer, FixedPoint2 chargeCost)
    {
        var refund = EnsureComp<YautjaCasterProjectileRefundComponent>(projectile);
        refund.Bracer = bracer.Owner;
        refund.ChargeCost = chargeCost;
        refund.Fired = false;
    }

    private string GetPowerFailureMessage(YautjaBracerComponent bracer, FixedPoint2 amount)
    {
        return Loc.GetString(
            "cmu-yautja-drain-power-failed",
            ("charge", (int) bracer.Charge),
            ("max", (int) bracer.MaxCharge),
            ("amount", (int) amount));
    }

    private void PopupMode(Entity<YautjaCasterComponent> ent, EntityUid user, LocId message)
    {
        PopupMode(ent, user, message, ent.Comp.CurrentMode);
    }

    private void PopupMode(Entity<YautjaCasterComponent> ent, EntityUid user, LocId message, int modeIndex)
    {
        var mode = GetMode(ent.Comp);
        if (modeIndex >= 0 && modeIndex < ent.Comp.Modes.Count)
            mode = ent.Comp.Modes[modeIndex];

        if (mode == null)
            return;

        var text = Loc.GetString(message, ("mode", Loc.GetString(mode.Name)));
        if (_net.IsClient)
            _popup.PopupPredicted(text, user, user, PopupType.Medium);
        else
            _popup.PopupClient(text, user, user, PopupType.Medium);
    }
}
