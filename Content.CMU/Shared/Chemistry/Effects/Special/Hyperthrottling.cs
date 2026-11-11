/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

/// <summary>
/// Overstimulates the language centers: while the chemical is in the metabolizer they can speak and
/// understand every language, including alien ones (Xenoish, Pathogen, Yautja, ...).
/// This is an on/off property, so potency does not scale anything. The grant is refreshed every tick
/// and lapses shortly after the chemical is gone.
/// </summary>
public sealed partial class Hyperthrottling : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    /// <summary>Seconds the grant lasts past the last metabolism tick. Ticks run once a second.</summary>
    [DataField]
    public float LingerSeconds = 6f;

    /// <summary>Language prototype IDs that are never granted.</summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>> ExcludedLanguages = new();

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => "Allows the user to [color=green]speak and understand every language[/color], including alien tongues.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond}[/color] brain damage.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 3}[/color] brain damage and cause unconsciousness.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.LanguageGrant.Apply(args.TargetEntity, TimeSpan.FromSeconds(LingerSeconds), ExcludedLanguages);

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 3f, ShockType);
        system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }
}
