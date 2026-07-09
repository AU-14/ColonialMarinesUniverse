using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Treatment.Effects;

[UsedImplicitly]
public sealed partial class HealOrganEffect : EntityEffect
{
    /// <summary>
    ///     Component name (the YAML <c>type:</c> value, e.g. <c>"Liver"</c>)
    ///     that the targeted organ must carry for the heal to land.
    /// </summary>
    [DataField(required: true)]
    public string OrganComponent = string.Empty;

    /// <summary>
    ///     HP healed per metabolize cycle (not per second).
    /// </summary>
    [DataField]
    public FixedPoint2 Amount = 1;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagent)
            return;

        var entMan = args.EntityManager;
        var compFactory = IoCManager.Resolve<IComponentFactory>();
        if (!compFactory.TryGetRegistration(OrganComponent, out var reg))
            return;

        var medicalIndex = entMan.System<CMUMedicalBodyIndexSystem>();
        var organSys = entMan.System<SharedOrganHealthSystem>();

        foreach (var organ in medicalIndex.GetOrgans(reagent.TargetEntity))
        {
            if (!entMan.HasComponent(organ.Owner, reg.Type))
                continue;
            organSys.HealOrgan((organ.Owner, null), reagent.TargetEntity, Amount);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-heal-organ-guidebook", ("organ", OrganComponent), ("amount", Amount));
}
