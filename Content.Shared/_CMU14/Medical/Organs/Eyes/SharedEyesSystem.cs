using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Eye.Blinding.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Organs.Eyes;

public abstract class SharedEyesSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodySystem Body = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EyesComponent, OrganStageChangedEvent>(OnStageChanged);
    }

    private void OnStageChanged(Entity<EyesComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        var bestStage = ComputeBestEyeStage(body);
        UpdateVisionStatus(body, bestStage);
    }

    /// <summary>
    ///     Best (lowest enum value) stage across all <see cref="EyesComponent"/>
    ///     organs in the body. A marine with one healthy eye → Healthy aggregate.
    /// </summary>
    protected OrganDamageStage ComputeBestEyeStage(EntityUid body)
    {
        var best = OrganDamageStage.Dead;
        var any = false;
        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!HasComp<EyesComponent>(organId))
                continue;
            if (!TryComp<OrganHealthComponent>(organId, out var oh))
                continue;
            if (!any || (byte)oh.Stage < (byte)best)
                best = oh.Stage;
            any = true;
        }
        return any ? best : OrganDamageStage.Healthy;
    }

    protected virtual void UpdateVisionStatus(EntityUid body, OrganDamageStage stage)
    {
    }
}
