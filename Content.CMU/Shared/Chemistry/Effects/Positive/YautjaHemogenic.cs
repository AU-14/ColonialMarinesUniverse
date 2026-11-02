/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Movement;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

// cmss13 hemogenic/predator: hemogenic without the nutrition cost. works on anyone, that's how dathwei helps a
// thrall's blood loss, and a non-yautja on a strong dose gets the same thick blood damage as hemogenic
public sealed partial class Yautjahemogenic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private const float MinimumHungerSatiation = 200f;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var baseText = $"Restores [color=green]{PotencyPerSecond * 2}[/color]cl of blood without consuming nutrition. " +
                       "Non-Yautja need to not be hungry for it to work.";

        return ActualPotency > 3
            ? $"{baseText}\nOn non-Yautja with too much blood, deals [color=red]{PotencyPerSecond}[/color] brute, " +
              $"[color=red]{PotencyPerSecond * 2}[/color] airloss damage, and slows them down."
            : baseText;
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        var yautja = system.HasYautja(target);

        if (!yautja &&
            (!system.TryGetSatiation(target, out var satiation) ||
             system.Satiation.GetValueOrNull((target, satiation), SatiationSystem.Hunger) is not { } hunger ||
             hunger < MinimumHungerSatiation))
        {
            return;
        }

        if (!system.TryGetBloodstream(target, out var bloodstream))
            return;

        system.Bloodstream
            .TryModifyBloodLevel((target, bloodstream), potency * 2f);

        if (yautja ||
            args.ActualPotency <= 3 ||
            !system.RMCBloodstream.TryGetBloodReadout(target, out var currentBlood, out _) ||
            currentBlood <= 570)
        {
            return;
        }

        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency;
        damage.DamageDict[AsphyxiationType] = potency * 2;
        damageable.TryChangeDamage(target, damage, true, interruptsDoAfters: false);
        // a collection expression into a List compiles to CollectionsMarshal.SetCount, which the client sandbox refuses
        var modifiers = new List<TemporarySpeedModifierSet>(1);
        modifiers.Add(new(TimeSpan.FromSeconds(2), 0.9f, 0.9f));
        system.TemporarySpeedModifiers.ModifySpeed(target, modifiers);
    }
}
