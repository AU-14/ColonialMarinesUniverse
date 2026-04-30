using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CMU14.Medical.EntityEffects;

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

        var bodySys = entMan.System<SharedBodySystem>();
        var heartSys = entMan.System<SharedHeartSystem>();
        foreach (var organ in bodySys.GetBodyOrgans(reagent.TargetEntity))
        {
            if (!entMan.TryGetComponent<HeartComponent>(organ.Id, out var heart))
                continue;
            if (!heart.Stopped)
                continue;
            if (entMan.TryGetComponent<OrganHealthComponent>(organ.Id, out var oh) &&
                oh.Stage == OrganDamageStage.Dead)
                continue;

            heartSys.TryRestartHeart((organ.Id, heart));
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-restart-heart-guidebook", ("chance", (int)(ChancePerTick * 100f)));
}
