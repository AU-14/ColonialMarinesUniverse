using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Organs.Kidneys;

public abstract class SharedKidneysSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId RenalFailure = "StatusEffectCMURenalFailure";

    private static readonly Dictionary<OrganDamageStage, float> FiltrationByStage = new()
    {
        { OrganDamageStage.Healthy, 1.0f  },
        { OrganDamageStage.Bruised, 0.85f },
        { OrganDamageStage.Damaged, 0.6f  },
        { OrganDamageStage.Failing, 0.3f  },
        { OrganDamageStage.Dead,    0.0f  },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KidneysComponent, OrganStageChangedEvent>(OnStageChanged);
    }

    private void OnStageChanged(Entity<KidneysComponent> ent, ref OrganStageChangedEvent args)
    {
        ent.Comp.WasteFiltration = FiltrationByStage[args.New];
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, RenalFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(body, RenalFailure);
    }

    /// <summary>
    ///     Pair survival via <c>Math.Max</c> across all kidneys. Missing-kidney
    ///     bodies return 1.0 unchanged.
    /// </summary>
    public float GetClearanceMultiplier(EntityUid body)
    {
        var best = -1f;
        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!TryComp<KidneysComponent>(organId, out var kidney))
                continue;
            if (kidney.WasteFiltration > best)
                best = kidney.WasteFiltration;
        }

        return best < 0f ? 1.0f : best;
    }
}
