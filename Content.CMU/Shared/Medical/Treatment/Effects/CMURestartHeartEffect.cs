using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

/// <summary>
///     A flatlined Dead-stage heart is past the point where chemistry can save it;
///     the surgeon must transplant.
/// </summary>
[UsedImplicitly]
public sealed partial class CMURestartHeartEffect : EntityEffectBase<CMURestartHeartEffect>
{
    [DataField]
    public float ChancePerTick = 0.05f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-restart-heart-guidebook", ("chance", (int)(ChancePerTick * 100f)));
}

public sealed partial class CMURestartHeartEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, CMURestartHeartEffect>
{
    [Dependency] private readonly CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private readonly SharedHeartSystem _heart = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<CMURestartHeartEffect> args)
    {
        if (args.ReagentContext == null || !_random.Prob(args.Effect.ChancePerTick))
            return;

        foreach (var organ in _medicalIndex.GetOrgans(entity))
        {
            if (!TryComp(organ.Owner, out HeartComponent? heart) || !heart.Stopped)
                continue;

            if (TryComp(organ.Owner, out OrganHealthComponent? health) && health.Stage == OrganDamageStage.Dead)
                continue;

            _heart.TryRestartHeart((organ.Owner, heart));
        }
    }
}
