/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Fluxing : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Dissolves embedded shrapnel at [color=green]{PotencyPerSecond}[/color] fragments of progress per second.\n" +
           $"Overdoses remove [color=red]{PotencyPerSecond}[/color] bone integrity and cause " +
           $"[color=red]{PotencyPerSecond * 2}[/color] brute plus [color=red]{PotencyPerSecond}[/color] toxin damage.\n" +
           "Critical overdoses rapidly damage and fracture the weakest bone while doubling the systemic damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var status = system.ChemicalPropertyStatus;
        var fluxing = status.ApplyFluxing(args.TargetEntity, (float)potency);
        if (fluxing == null)
            return;

        var count = (int)MathF.Floor(fluxing.Progress);
        if (count <= 0)
            return;

        var removed = system.Shrapnel
            .TryRemoveShrapnel(args.TargetEntity, count);
        fluxing.Progress -= removed;
        system.DirtyFluxing(args.TargetEntity, fluxing);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.Bone
            .DamageWeakestBone(args.TargetEntity, potency, fracture: false);
        ApplyBodyDamage(damageable, potency * 2f, potency, args);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.Bone
            .DamageWeakestBone(args.TargetEntity, potency * 25f, fracture: true);
        ApplyBodyDamage(damageable, potency * 2f, potency * 2f, args);
    }

    private static void ApplyBodyDamage(DamageableSystem damageable, FixedPoint2 blunt, FixedPoint2 poison,
        RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = blunt;
        damage.DamageDict[PoisonType] = poison;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
