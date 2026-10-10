using Content.Shared.Damage.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

/// <summary>
///     Thwei is the Yautja hemostatic from the movie self-surgery scenes,
///     ported from CM-SS13's bleeding datum: while it metabolizes, surface
///     bleeding stops without treating the wounds, internal bleeds are
///     removed, and blood volume returns via the hemogenic effect. The
///     medicomp surgery heals the damage pools, never this effect.
/// </summary>
public sealed partial class YautjaWoundHealing : RMCChemicalEffect
{
    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var medicalIndex = system.MedicalBodyIndex;
        var wounds = system.Wounds;
        foreach (var part in medicalIndex.GetBodyParts(args.TargetEntity))
        {
            // Flat and presence-gated like the DM source, not potency-scaled
            wounds.StopSurfaceBleedingOnPart(part);
            wounds.ClearInternalBleed(part);
        }
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Stops blood loss while it is in your system: surface bleeding is staunched and internal bleeding removed, but wounds stay open until treated.";
    }
}
