using System.Collections.Generic;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Bones;

public static class FractureProfile
{
    public readonly record struct Profile(
        float MovementMult,
        float AimSwayMult,
        FixedPoint2 PainPerSecond,
        FixedPoint2 BloodlossPerSecond,
        bool DisablesAffectedActions);

    private static readonly Dictionary<FractureSeverity, Profile> Table = new()
    {
        [FractureSeverity.None] = new(1.00f, 1.00f, 0, 0, false),
        [FractureSeverity.Hairline] = new(0.95f, 1.10f, 1, 0, false),
        [FractureSeverity.Simple] = new(0.85f, 1.25f, 2, 0, false),
        [FractureSeverity.Compound] = new(0.70f, 1.40f, 3, 0.5f, false),
        [FractureSeverity.Comminuted] = new(0.40f, 2.00f, 5, 1.0f, true),
    };

    public static Profile Get(FractureSeverity sev)
        => Table.TryGetValue(sev, out var profile) ? profile : Table[FractureSeverity.None];
}
