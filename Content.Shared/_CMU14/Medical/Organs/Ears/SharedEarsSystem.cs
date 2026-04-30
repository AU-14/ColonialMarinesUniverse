using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Organs.Ears;

public abstract class SharedEarsSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Tinnitus = "StatusEffectCMUTinnitus";
    private static readonly EntProtoId Deafened = "StatusEffectCMUDeafened";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EarsComponent, OrganStageChangedEvent>(OnStageChanged);
    }

    private void OnStageChanged(Entity<EarsComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        var bestStage = ComputeBestEarStage(body);
        ApplyHearingStatus(body, bestStage);
    }

    private OrganDamageStage ComputeBestEarStage(EntityUid body)
    {
        var best = OrganDamageStage.Dead;
        var any = false;
        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!HasComp<EarsComponent>(organId))
                continue;
            if (!TryComp<OrganHealthComponent>(organId, out var oh))
                continue;
            if (!any || (byte)oh.Stage < (byte)best)
                best = oh.Stage;
            any = true;
        }
        return any ? best : OrganDamageStage.Healthy;
    }

    private void ApplyHearingStatus(EntityUid body, OrganDamageStage stage)
    {
        Status.TryRemoveStatusEffect(body, Tinnitus);
        Status.TryRemoveStatusEffect(body, Deafened);

        switch (stage)
        {
            case OrganDamageStage.Damaged:
            case OrganDamageStage.Failing:
                Status.TrySetStatusEffectDuration(body, Tinnitus, duration: null);
                break;
            case OrganDamageStage.Dead:
                Status.TrySetStatusEffectDuration(body, Deafened, duration: null);
                break;
        }
    }
}
