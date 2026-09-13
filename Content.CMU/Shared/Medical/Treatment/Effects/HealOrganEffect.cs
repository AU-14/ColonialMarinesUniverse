using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

[UsedImplicitly]
public sealed partial class HealOrganEffect : EntityEffectBase<HealOrganEffect>
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

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-heal-organ-guidebook", ("organ", OrganComponent), ("amount", Amount));
}

public sealed partial class HealOrganEntityEffectSystem : EntityEffectSystem<MetaDataComponent, HealOrganEffect>
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private readonly SharedOrganHealthSystem _organHealth = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<HealOrganEffect> args)
    {
        if (args.ReagentContext == null ||
            !_componentFactory.TryGetRegistration(args.Effect.OrganComponent, out var registration))
        {
            return;
        }

        foreach (var organ in _medicalIndex.GetOrgans(entity))
        {
            if (!HasComp(organ.Owner, registration.Type))
                continue;

            _organHealth.HealOrgan((organ.Owner, null), entity, args.Effect.Amount);
        }
    }
}
