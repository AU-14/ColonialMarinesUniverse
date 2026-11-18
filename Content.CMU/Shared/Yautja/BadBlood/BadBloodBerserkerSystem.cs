using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-605: the Berserker's body. Raises its crit and dead thresholds once, cuts the damage it takes and its
/// sprint. The longer stagger is read by BadBloodMeleeSystem and the stun bolt refusal by BadBloodEradicatorSystem.
/// </summary>
public sealed class BadBloodBerserkerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodBerserkerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BadBloodBerserkerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BadBloodBerserkerComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<BadBloodBerserkerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnStartup(Entity<BadBloodBerserkerComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);

        // the thresholds are networked with the body, so only the server raises them
        if (_net.IsClient)
            return;

        // CMU14 ECS-703: the Blood Rage action comes and goes with the caste
        _actions.AddAction(ent.Owner, ref ent.Comp.RageActionEntity, ent.Comp.RageAction);
        Dirty(ent);

        // a body without thresholds has nothing to raise, and the flag stays false so shutdown takes nothing off
        if (ent.Comp.ThresholdsRaised || !TryComp<MobThresholdsComponent>(ent, out var thresholds))
            return;

        ent.Comp.ThresholdsRaised = true;
        ShiftThresholds(ent, ent.Comp.ThresholdBonus, thresholds);
    }

    private void OnShutdown(Entity<BadBloodBerserkerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _movement.RefreshMovementSpeedModifiers(ent.Owner);

        if (_net.IsClient)
            return;

        _actions.RemoveAction(ent.Owner, ent.Comp.RageActionEntity);
        RemComp<BadBloodBloodRageComponent>(ent.Owner);

        if (!ent.Comp.ThresholdsRaised)
            return;

        ent.Comp.ThresholdsRaised = false;
        if (TryComp<MobThresholdsComponent>(ent, out var thresholds))
            ShiftThresholds(ent, -ent.Comp.ThresholdBonus, thresholds);
    }

    private void ShiftThresholds(EntityUid uid, int amount, MobThresholdsComponent thresholds)
    {
        foreach (var state in new[] { MobState.Critical, MobState.Dead })
        {
            if (_thresholds.TryGetThresholdForState(uid, state, out var threshold, thresholds))
                _thresholds.SetMobStateThreshold(uid, threshold.Value + FixedPoint2.New(amount), state, thresholds);
        }
    }

    // only incoming damage, healing goes through untouched
    private void OnDamageModify(Entity<BadBloodBerserkerComponent> ent, ref DamageModifyEvent args)
    {
        DamageSpecifier? modified = null;
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            modified ??= new DamageSpecifier(args.Damage);
            modified.DamageDict[type] = amount * ent.Comp.DamageTakenMultiplier;
        }

        if (modified != null)
            args.Damage = modified;
    }

    private void OnRefreshSpeed(Entity<BadBloodBerserkerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // the refresh on shutdown must already leave the slowdown out
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.ModifySpeed(1f, ent.Comp.SprintMultiplier);
    }
}
