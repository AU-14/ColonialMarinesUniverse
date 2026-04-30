using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Organs.Stomach;

public abstract class SharedStomachSystem : EntitySystem
{
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Nausea = "StatusEffectCMUNausea";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUStomachComponent, OrganStageChangedEvent>(OnStageChanged);
    }

    private void OnStageChanged(Entity<CMUStomachComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, Nausea, duration: null);
        else
            Status.TryRemoveStatusEffect(body, Nausea);
    }
}
