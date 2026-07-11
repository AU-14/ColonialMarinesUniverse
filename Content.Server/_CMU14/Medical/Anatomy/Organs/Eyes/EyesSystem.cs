using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Eyes;

public sealed partial class EyesSystem : SharedEyesSystem
{
    [Dependency] private BlindableSystem _blindable = default!;

    protected override void UpdateVisionStatus(EntityUid body, OrganDamageStage stage)
    {
        if (stage == OrganDamageStage.Dead)
            EnsureComp<CMUOrganBlindnessComponent>(body);
        else
            RemComp<CMUOrganBlindnessComponent>(body);

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
            // Dead → 0: CMUOrganBlindnessComponent dominates, and zeroing our
            // contribution keeps damage clean if the eyes are revived.
            _ => 0,
        };
    }
}
