using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Penalties;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Medical.Penalties;

public sealed class CMUAccuracyEventSubscriber : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly CMGunSystem _gun = default!;

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCWeaponAccuracyComponent, GetWeaponAccuracyEvent>(OnGetAccuracy);
        SubscribeLocalEvent<CMUMedicalGunAimPenaltyComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
    }

    private void OnGetAccuracy(Entity<RMCWeaponAccuracyComponent> weapon, ref GetWeaponAccuracyEvent args)
    {
        if (!TryGetAimPenalty(weapon.Owner, out var aim))
            return;

        var sway = aim.SwayMultiplier;
        if (sway <= 1.0f)
            return;

        // Floor at 0.1x so a fully-debuffed marine can still hit something
        // at point blank.
        args.AccuracyMultiplier = (FixedPoint2)System.Math.Max(0.1, (double)args.AccuracyMultiplier / sway);
    }

    private void OnGunRefreshModifiers(Entity<CMUMedicalGunAimPenaltyComponent> weapon, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetAimPenalty(weapon.Owner, out var aim))
            return;

        var spread = aim.SpreadMultiplier;
        if (spread <= 1.0f)
            return;

        args.AngleIncrease = ScaleAngle(args.AngleIncrease, spread);
        args.MinAngle = ScaleAngle(args.MinAngle, spread);
        args.MaxAngle = ScaleAngle(args.MaxAngle, spread);
    }

    private bool TryGetAimPenalty(EntityUid weapon, out CMUAimAccuracyComponent aim)
    {
        aim = default!;

        if (!_medicalEnabled || !_statusEffectsEnabled)
            return false;

        if (!_gun.TryGetGunUser(weapon, out var user))
            return false;

        if (!HasComp<CMUHumanMedicalComponent>(user.Owner))
            return false;

        if (!TryComp(user.Owner, out CMUAimAccuracyComponent? component))
            return false;

        aim = component;
        return true;
    }

    private static Angle ScaleAngle(Angle angle, float multiplier)
        => new(angle.Theta * multiplier);
}
