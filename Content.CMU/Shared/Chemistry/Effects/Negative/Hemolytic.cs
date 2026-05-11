/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Emote;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CMU14.Chemistry.Effects.Negative;

public sealed partial class Hemolytic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private static readonly ProtoId<EmotePrototype> GaspEmote = "Gasp";
    private static readonly ProtoId<EmotePrototype> YawnEmote = "Yawn";
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"DESCRIPTION IS NOT IMPLEMENTED";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var targ = args.TargetEntity;

        if (entman.TryGetComponent<BloodstreamComponent>(targ, out var blood))
        {
            var bloodsys = entman.System<SharedBloodstreamSystem>();
            bloodsys.TryModifyBloodLevel((targ, blood), -(5.0 * potency));
        }
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var targ = args.TargetEntity;

        if (entman.TryGetComponent<BloodstreamComponent>(targ, out var blood))
        {
            var bloodsys = entman.System<SharedBloodstreamSystem>();
            bloodsys.TryModifyBloodLevel((targ, blood), -(4.0 * PotencyPerSecond));
            //TODO M.drowsiness

            var random = IoCManager.Resolve<IRobustRandom>();
            if (!random.Prob(0.1f))
                return;

            var emoteSystem = args.EntityManager.System<SharedRMCEmoteSystem>();
            if (random.Prob(0.5f))
            {
                emoteSystem.TryEmoteWithChat(
                args.TargetEntity,
                GaspEmote,
                hideLog: true,
                ignoreActionBlocker: true,
                forceEmote: true
                );
            }
            else
            {
                emoteSystem.TryEmoteWithChat(
                args.TargetEntity,
                YawnEmote,
                hideLog: true,
                ignoreActionBlocker: true,
                forceEmote: true
                );
            }
            
        }
    }
    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[AsphyxiationType] = potency * 5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }


}
