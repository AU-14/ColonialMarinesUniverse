/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Neuropeutic : OrganPeuticEffect<CMUBrainComponent>
{
    protected override string OrganName => "brain";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Shock";
    protected override string PlantEffect => "Forces species mutation in plants.";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => base.ReagentEffectGuidebookText(prototype, entSys) +
           $" Critical overdoses additionally stun for [color=red]{PotencyPerSecond * 2}[/color] seconds per tick.";

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
        base.TickCriticalOverdose(system, damageable, potency, args);
        system.Stun.TryStun(
            args.TargetEntity,
            TimeSpan.FromSeconds((float)potency * 2f),
            true);
    }

    protected override void TickHydroTray(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.RaiseHydroTick<Neuropeutic>(args.TargetEntity, potency, args.Context.Quantity);
    }
}
