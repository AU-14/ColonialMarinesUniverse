using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Anatomy.Bones;

public static class FractureProfile
{
    public readonly record struct Profile(
        float MovementMult,
        float AimSwayMult,
        FixedPoint2 PainPerSecond,
        FixedPoint2 BloodlossPerSecond,
        bool DisablesAffectedActions,
        float MovementInternalBleedChance,
        float MovementOrganDamageChance,
        FixedPoint2 MovementOrganDamage,
        float MovementInternalBleedRate,
        float MovementCheckCooldownSeconds);

    public static Profile Get(FractureSeverity sev) => sev switch
    {
        FractureSeverity.Hairline => new(
            0.95f, 1.02f, 1, 0, false,
            0.02f, 0.01f, 1, 0.2f, 2f),
        FractureSeverity.Simple => new(
            0.85f, 1.05f, 2, 0, false,
            0.05f, 0.03f, 2, 0.3f, 2f),
        FractureSeverity.Compound => new(
            0.70f, 1.10f, 3, 0, false,
            0.10f, 0.08f, 3, 0.5f, 2f),
        FractureSeverity.Shattered => new(
            0.40f, 1.20f, 5, 0.5f, true,
            0.20f, 0.15f, 5, 0.75f, 2f),
        _ => new(
            1.00f, 1.00f, 0, 0, false,
            0f, 0f, 0, 0f, 2f),
    };
}
