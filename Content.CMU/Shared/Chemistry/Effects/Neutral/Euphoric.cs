/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Euphoric : RMCChemicalEffect
{
    private static readonly ProtoId<EmotePrototype> LaughEmote = "Laugh";
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Suppresses [color=green]{MathF.Min(40f, LinearLevel * 10f)}%[/color] of new pain and causes occasional laughter.\n" +
           $"Overdoses cause drowsiness.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond}[/color] brain damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        system.PainShock.AddPainSuppressionProfile(target, MathF.Min(0.4f, args.LinearLevel * 0.1f), 1, args.ActualPotency * 0.25f, TimeSpan.FromSeconds(3), 0.1f);
        if (system.Random.Prob(0.05f))
            system.RMCEmote.TryEmoteWithChat(target, LaughEmote, hideLog: true, ignoreActionBlocker: true, forceEmote: true);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Drowsiness, TimeSpan.FromSeconds(3));

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);
}
