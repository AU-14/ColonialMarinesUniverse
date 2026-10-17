using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;

namespace Content.Server.CMU14.Medical.Treatment.Effects;

public sealed partial class CMUCryogenicBoneAndBloodRepairEntityEffectSystem
    : EntityEffectSystem<CMUHumanMedicalComponent, CMUCryogenicBoneAndBloodRepair>
{
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    protected override void Effect(Entity<CMUHumanMedicalComponent> entity,
        ref EntityEffectEvent<CMUCryogenicBoneAndBloodRepair> args)
    {
        if (args.ReagentContext is not { Origin: ReagentEffectOrigin.Metabolism } context ||
            context.Quantity.Quantity <= FixedPoint2.Zero)
            return;

        _bone.CryogenicallyMendFractures(entity.Owner,
            args.Effect.BoneIntegrityPerUnit * context.Quantity.Quantity);

        if (!TryComp<BloodstreamComponent>(entity, out var blood))
            return;

        // The effect runs once per metabolism interval while Necrosol is present.
        // Use the interval to keep blood recovery at 1% per second.
        var tickSeconds = context.Organ is { } metabolizerUid &&
                          TryComp<MetabolizerComponent>(metabolizerUid, out var metabolizer)
            ? (float) metabolizer.AdjustedUpdateInterval.TotalSeconds
            : 1f;
        var fraction = args.Effect.BloodPercentPerSecond / 100f * tickSeconds;
        var normalVolume = _bloodstream.GetReferenceVolume((entity.Owner, blood));
        _bloodstream.TryRegulateBloodLevel((entity.Owner, blood), normalVolume * (FixedPoint2) fraction);
    }
}

public sealed partial class CMUCryogenicWoundRepairEntityEffectSystem
    : EntityEffectSystem<CMUHumanMedicalComponent, CMUCryogenicWoundRepair>
{
    [Dependency] private CMUWoundsSystem _wounds = default!;

    protected override void Effect(Entity<CMUHumanMedicalComponent> entity,
        ref EntityEffectEvent<CMUCryogenicWoundRepair> args)
    {
        if (args.ReagentContext is not { Origin: ReagentEffectOrigin.Metabolism } context ||
            context.Quantity.Quantity <= FixedPoint2.Zero)
            return;

        _wounds.CryogenicallyHealWounds(entity.Owner, args.Effect.Type,
            args.Effect.AmountPerUnit * context.Quantity.Quantity);
    }
}

public sealed partial class CMUCryogenicOrganRepairEntityEffectSystem
    : EntityEffectSystem<CMUHumanMedicalComponent, CMUCryogenicOrganRepair>
{
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;

    protected override void Effect(Entity<CMUHumanMedicalComponent> entity,
        ref EntityEffectEvent<CMUCryogenicOrganRepair> args)
    {
        if (args.ReagentContext is not { Origin: ReagentEffectOrigin.Metabolism } context ||
            context.Quantity.Quantity <= FixedPoint2.Zero)
            return;

        var amount = args.Effect.AmountPerUnit * context.Quantity.Quantity;
        foreach (var (organ, _) in _medicalIndex.GetOrgans(entity.Owner))
        {
            if (!TryComp<OrganHealthComponent>(organ, out var health))
                continue;

            _organHealth.HealOrgan((organ, health), entity.Owner, amount);
        }
    }
}
