using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Medical.Treatment.Effects;

public sealed partial class CMURegenerateLimbEntityEffectSystem
    : EntityEffectSystem<CMUHumanMedicalComponent, CMURegenerateLimb>
{
    private static readonly FixedPoint2 DosePerLimb = FixedPoint2.New(5);

    [Dependency] private CMUBodyScannerReadoutSystem _readout = default!;
    [Dependency] private CMUSurgerySystem _surgery = default!;

    protected override void Effect(Entity<CMUHumanMedicalComponent> entity, ref EntityEffectEvent<CMURegenerateLimb> args)
    {
        if (args.ReagentContext is not { Origin: ReagentEffectOrigin.Metabolism } context
            || context.Quantity.Quantity <= FixedPoint2.Zero)
            return;

        var missing = _readout.GetMissingLimbSlots(entity.Owner);
        if (missing.Count == 0)
        {
            RemComp<CMUNecrosolRegenerationComponent>(entity.Owner);
            return;
        }

        var progress = EnsureComp<CMUNecrosolRegenerationComponent>(entity.Owner);
        progress.AccumulatedUnits += context.Quantity.Quantity;

        while (progress.AccumulatedUnits >= DosePerLimb)
        {
            var regrown = false;
            foreach (var (type, symmetry) in missing)
            {
                if (!_surgery.TryGetMissingPartSite(entity.Owner, type, symmetry, out var parent, out var slot))
                    continue;

                if (!_surgery.TryRegenerateLimb(entity.Owner, type, symmetry, parent, slot, () => true))
                    continue;

                regrown = true;
                break;
            }

            if (!regrown)
                return;

            progress.AccumulatedUnits -= DosePerLimb;
            missing = _readout.GetMissingLimbSlots(entity.Owner);
            if (missing.Count == 0)
            {
                RemComp<CMUNecrosolRegenerationComponent>(entity.Owner);
                return;
            }
        }
    }
}
