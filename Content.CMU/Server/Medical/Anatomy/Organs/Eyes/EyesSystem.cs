using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;

namespace Content.Server.CMU14.Medical.Anatomy.Organs.Eyes;

public sealed partial class EyesSystem : SharedEyesSystem
{
    protected override void UpdateVisionStatus(EntityUid body, OrganDamageStage stage)
    {
        if (stage == OrganDamageStage.Dead)
            EnsureComp<CMUOrganBlindnessComponent>(body);
        else
            RemComp<CMUOrganBlindnessComponent>(body);

        SetOrganBlur(body, StageToBlur(stage));
    }

    private static float StageToBlur(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Bruised => 1,
            OrganDamageStage.Damaged => 2,
            OrganDamageStage.Failing => 3,
            // Dead eyes contribute independent blindness instead of blur.
            _ => 0,
        };
    }
}
