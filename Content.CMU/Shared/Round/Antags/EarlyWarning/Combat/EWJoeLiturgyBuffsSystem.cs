using Content.Shared.CMU14.Movement;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-12: the passive half of the Liturgy meter. Each stack makes the Joe's fists and machete swing a little
/// faster and shortens the slows, superslows and roots it takes. Shared so the owner predicts its own attack
/// rate from the owner-networked <see cref="EWJoeLiturgyComponent.Stacks"/>; other clients never compute this
/// Joe's rate. Durations only: nothing here changes movement speed, so no speed refresh is needed.
/// </summary>
/// <remarks>
/// Subscriptions (all pairs were free): (EWJoeLiturgyComponent, GetMeleeAttackRateEvent) for fists, where the
/// Joe is its own weapon; (EWJoeMacheteComponent, GetMeleeAttackRateEvent), raised on the machete, reading the
/// stacks of the user; (EWJoeLiturgyComponent, CMUGetSlowDurationMultiplierEvent), raised by the RMC slow hooks.
/// </remarks>
public sealed class EWJoeLiturgyBuffsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeLiturgyComponent, GetMeleeAttackRateEvent>(OnFistAttackRate);
        SubscribeLocalEvent<EWJoeMacheteComponent, GetMeleeAttackRateEvent>(OnMacheteAttackRate);
        SubscribeLocalEvent<EWJoeLiturgyComponent, CMUGetSlowDurationMultiplierEvent>(OnSlowDuration);
    }

    private void OnFistAttackRate(Entity<EWJoeLiturgyComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        // raised on the weapon: only a bare-handed Joe is its own weapon
        if (args.Weapon != ent.Owner)
            return;

        args.Multipliers *= AttackRateMultiplier(ent.Comp);
    }

    private void OnMacheteAttackRate(Entity<EWJoeMacheteComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (!TryComp(args.User, out EWJoeLiturgyComponent? liturgy))
            return;

        args.Multipliers *= AttackRateMultiplier(liturgy);
    }

    private void OnSlowDuration(Entity<EWJoeLiturgyComponent> ent, ref CMUGetSlowDurationMultiplierEvent args)
    {
        args.Multiplier *= SlowDurationMultiplier(ent.Comp);
    }

    /// <summary>1 + <see cref="EWJoeLiturgyComponent.AttackRatePerStack"/> per stack.</summary>
    public static float AttackRateMultiplier(EWJoeLiturgyComponent liturgy)
    {
        return 1f + liturgy.AttackRatePerStack * Math.Max(0, liturgy.Stacks);
    }

    /// <summary>1 - <see cref="EWJoeLiturgyComponent.SlowReductionPerStack"/> per stack, never below zero.</summary>
    public static float SlowDurationMultiplier(EWJoeLiturgyComponent liturgy)
    {
        return MathF.Max(0f, 1f - liturgy.SlowReductionPerStack * Math.Max(0, liturgy.Stacks));
    }
}
