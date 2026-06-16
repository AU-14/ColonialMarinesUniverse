using Content.Shared.Actions;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._CMU14.Xenonids.Bull;

public sealed partial class CMUXenoBullChargeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private RMCDazedSystem _daze = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private XenoChargeSystem _xenoCharge = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoBullChargeComponent, CMUXenoBullChargeActionEvent>(OnBullChargeAction);
        SubscribeLocalEvent<CMUXenoBullChargeTargetComponent, XenoToggleChargingCollideEvent>(OnBullChargeCollide);
    }

    private void OnBullChargeAction(Entity<CMUXenoBullChargeComponent> bull, ref CMUXenoBullChargeActionEvent args)
    {
        if (args.Handled)
            return;

        bull.Comp.Mode = args.Mode;
        Dirty(bull);
        UpdateModeActions(bull, args.Mode);
        _popup.PopupClient(Loc.GetString(GetModePopup(args.Mode)), bull, bull, PopupType.Small);
        args.Handled = true;
    }

    private void OnBullChargeCollide(Entity<CMUXenoBullChargeTargetComponent> target, ref XenoToggleChargingCollideEvent args)
    {
        if (args.Charger.Comp.Stage <= 0 ||
            !TryComp<CMUXenoBullChargeComponent>(args.Charger, out var bull) ||
            !_xeno.CanAbilityAttackTarget(args.Charger.Owner, target.Owner))
        {
            return;
        }

        var maxStage = TryComp<XenoToggleChargingComponent>(args.Charger, out var charging)
            ? charging.MaxStage
            : args.Charger.Comp.Stage;

        var scaledDamage = ScaleDamage(GetDamage(bull, bull.Mode), args.Charger.Comp.Stage, maxStage);
        _damageable.TryChangeDamage(target, scaledDamage, origin: args.Charger, tool: args.Charger);
        args.Handled = ShouldHandleImpact(bull.Mode);
        if (ShouldPlayImpactSound(bull.Mode))
            _audio.PlayPredicted(GetImpactSound(bull, bull.Mode), target, args.Charger);

        var origin = _transform.GetMapCoordinates(args.Charger);
        switch (bull.Mode)
        {
            case CMUXenoBullChargeMode.Plow:
                _size.KnockBack(target, origin, bull.PlowKnockback, bull.PlowKnockback, bull.KnockbackSpeed, true);
                break;
            case CMUXenoBullChargeMode.Headbutt:
                _stun.TryParalyze(target, TimeSpan.FromSeconds(GetStageScaledSeconds(bull.HeadbuttParalyze, args.Charger.Comp.Stage, maxStage)), true);
                _size.KnockBack(target, origin, bull.HeadbuttKnockback, bull.HeadbuttKnockback, bull.KnockbackSpeed, true);
                _xenoCharge.CMUEndToggleCharging(args.Charger, false);
                break;
            case CMUXenoBullChargeMode.Gore:
                _slow.TrySlowdown(target, TimeSpan.FromSeconds(GetStageScaledSeconds(bull.GoreSlowdown, args.Charger.Comp.Stage, maxStage)));
                _daze.TryDaze(target, TimeSpan.FromSeconds(GetGoreStaggerSeconds(args.Charger.Comp.Stage, maxStage, bull.GoreStagger)), true, stutter: true);
                var injected = TryInjectGoreReagent(target, bull);
                _size.KnockBack(target, origin, bull.GoreKnockback, bull.GoreKnockback, bull.KnockbackSpeed, true);
                if (ShouldPlayGoreSpraySound(injected))
                    _audio.PlayPredicted(bull.GoreSpraySound, target, args.Charger);
                _xenoCharge.CMUEndToggleCharging(args.Charger, false);
                break;
        }
    }

    private bool TryInjectGoreReagent(EntityUid target, CMUXenoBullChargeComponent bull)
    {
        if (bull.GoreReagentAmount <= FixedPoint2.Zero ||
            !_solution.TryGetInjectableSolution(target, out var solutionEnt, out _))
        {
            return false;
        }

        var available = solutionEnt.Value.Comp.Solution.AvailableVolume;
        if (available < bull.GoreReagentAmount)
            _solution.SplitSolution(solutionEnt.Value, bull.GoreReagentAmount - available);

        return _solution.TryAddReagent(solutionEnt.Value, bull.GoreReagent, bull.GoreReagentAmount);
    }

    private void UpdateModeActions(EntityUid bull, CMUXenoBullChargeMode mode)
    {
        foreach (var action in _rmcActions.GetActionsWithEvent<CMUXenoBullChargeActionEvent>(bull))
        {
            if (_actions.GetEvent(action) is CMUXenoBullChargeActionEvent bullAction)
                _actions.SetToggled((action, action), bullAction.Mode == mode);
        }
    }

    public static bool ShouldStopAfterImpact(CMUXenoBullChargeMode mode)
    {
        return mode != CMUXenoBullChargeMode.Plow;
    }

    public static bool ShouldHandleImpact(CMUXenoBullChargeMode mode)
    {
        return true;
    }

    public static bool ShouldPlayImpactSound(CMUXenoBullChargeMode mode)
    {
        return true;
    }

    public static bool ShouldPlayGoreSpraySound(bool injectionSucceeded)
    {
        return injectionSucceeded;
    }

    public static double GetGoreStaggerSeconds(int stage, int maxStage, TimeSpan duration)
    {
        return GetStageScaledSeconds(duration, stage, maxStage);
    }

    public static double GetStageScaledSeconds(TimeSpan duration, int stage, int maxStage)
    {
        if (stage <= 0 || maxStage <= 0)
            return 0;

        return duration.TotalSeconds * Math.Min(stage, maxStage) / maxStage;
    }

    private static DamageSpecifier GetDamage(CMUXenoBullChargeComponent bull, CMUXenoBullChargeMode mode)
    {
        return mode switch
        {
            CMUXenoBullChargeMode.Plow => bull.PlowDamage,
            CMUXenoBullChargeMode.Headbutt => bull.HeadbuttDamage,
            CMUXenoBullChargeMode.Gore => bull.GoreDamage,
            _ => bull.PlowDamage,
        };
    }

    private static SoundSpecifier GetImpactSound(CMUXenoBullChargeComponent bull, CMUXenoBullChargeMode mode)
    {
        return mode == CMUXenoBullChargeMode.Gore
            ? bull.GoreImpactSound
            : bull.ImpactSound;
    }

    private static DamageSpecifier ScaleDamage(DamageSpecifier damage, int stage, int maxStage)
    {
        var scale = maxStage <= 0
            ? FixedPoint2.Zero
            : FixedPoint2.New(Math.Min(stage, maxStage)) / FixedPoint2.New(maxStage);

        return damage * scale;
    }

    private static string GetModePopup(CMUXenoBullChargeMode mode)
    {
        return mode switch
        {
            CMUXenoBullChargeMode.Plow => "cmu-xeno-bull-mode-plow-popup",
            CMUXenoBullChargeMode.Headbutt => "cmu-xeno-bull-mode-headbutt-popup",
            CMUXenoBullChargeMode.Gore => "cmu-xeno-bull-mode-gore-popup",
            _ => "cmu-xeno-bull-mode-plow-popup",
        };
    }
}
