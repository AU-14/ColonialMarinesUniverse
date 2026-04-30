using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Penalties;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

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

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
    }

    private void OnGetAccuracy(Entity<RMCWeaponAccuracyComponent> weapon, ref GetWeaponAccuracyEvent args)
    {
        if (!_medicalEnabled || !_statusEffectsEnabled)
            return;

        if (!_gun.TryGetGunUser(weapon.Owner, out var user))
            return;

        if (!HasComp<CMUHumanMedicalComponent>(user.Owner))
            return;

        if (!TryComp<CMUAimAccuracyComponent>(user.Owner, out var aim))
            return;

        var sway = aim.SwayMultiplier;
        if (sway <= 1.0f)
            return;

        // Floor at 0.1× so a fully-debuffed marine can still hit something
        // at point blank.
        args.AccuracyMultiplier = (FixedPoint2)System.Math.Max(0.1, (double)args.AccuracyMultiplier / sway);
    }
}
