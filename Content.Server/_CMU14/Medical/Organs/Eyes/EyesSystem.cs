using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Eyes;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;

namespace Content.Server._CMU14.Medical.Organs.Eyes;

public sealed class EyesSystem : SharedEyesSystem
{
    [Dependency] private readonly BlindableSystem _blindable = default!;

    protected override void UpdateVisionStatus(EntityUid body, OrganDamageStage stage)
    {
        if (stage == OrganDamageStage.Dead)
            EnsureComp<TemporaryBlindnessComponent>(body);
        else
            RemComp<TemporaryBlindnessComponent>(body);

        ApplyEyeDamageContribution(body, StageToEyeDamage(stage));
    }

    private void ApplyEyeDamageContribution(EntityUid body, int desired)
    {
        if (!TryComp<BlindableComponent>(body, out var blindable))
            return;

        var tracker = EnsureComp<CMUEyeDamageContributionComponent>(body);
        var delta = desired - tracker.Applied;
        if (delta == 0)
            return;

        _blindable.AdjustEyeDamage((body, blindable), delta);
        tracker.Applied = desired;
    }

    private static int StageToEyeDamage(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 1,
            OrganDamageStage.Damaged => 2,
            OrganDamageStage.Failing => 3,
            // Dead → 0: TemporaryBlindnessComponent dominates, and zeroing our
            // contribution keeps damage clean if the eyes are revived.
            _ => 0,
        };
    }
}
