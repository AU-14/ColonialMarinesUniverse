using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Organs.Eyes;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;

namespace Content.Server._CMU14.Medical.Human.Organs.Eyes;

public sealed partial class EyesSystem : SharedEyesSystem
{
    [Dependency] private BlindableSystem _blindable = default!;

    protected override void UpdateVisionStatus(EntityUid body, OrganDamageStatus status)
    {
        if (status == OrganDamageStatus.Broken)
            EnsureComp<TemporaryBlindnessComponent>(body);
        else
            RemComp<TemporaryBlindnessComponent>(body);

        ApplyEyeDamageContribution(body, StatusToEyeDamage(status));
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

    private static int StatusToEyeDamage(OrganDamageStatus status)
    {
        return status switch
        {
            OrganDamageStatus.LittleBruised => 1,
            OrganDamageStatus.Bruised => 2,
            _ => 0,
        };
    }
}
