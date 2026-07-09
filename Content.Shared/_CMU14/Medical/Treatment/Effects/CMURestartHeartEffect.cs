using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CMU14.Medical.Treatment.Effects;

/// <summary>
///     A flatlined Dead-stage heart is past the point where chemistry can save it;
///     the surgeon must transplant.
/// </summary>
[UsedImplicitly]
public sealed partial class CMURestartHeartEffect : EntityEffect
{
    [DataField]
    public float ChancePerTick = 0.05f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagent)
            return;
        var entMan = args.EntityManager;
        var random = IoCManager.Resolve<IRobustRandom>();
        if (!random.Prob(ChancePerTick))
            return;

        var medicalIndex = entMan.System<CMUMedicalBodyIndexSystem>();
        var heartSys = entMan.System<SharedHeartSystem>();
        foreach (var organ in medicalIndex.GetOrgans(reagent.TargetEntity))
        {
            if (!entMan.TryGetComponent<HeartComponent>(organ.Owner, out var heart))
                continue;
            if (!heart.Stopped)
                continue;
            if (entMan.TryGetComponent<OrganHealthComponent>(organ.Owner, out var oh) &&
                oh.Stage == OrganDamageStage.Dead)
                continue;

            heartSys.TryRestartHeart((organ.Owner, heart));
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-restart-heart-guidebook", ("chance", (int)(ChancePerTick * 100f)));
}
