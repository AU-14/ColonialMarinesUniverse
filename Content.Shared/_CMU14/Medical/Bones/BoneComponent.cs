using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Bones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedBoneSystem))]
public sealed partial class BoneComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Integrity = 100;

    [DataField, AutoNetworkedField]
    public FixedPoint2 IntegrityMax = 100;

    /// <summary>
    ///     Fraction of post-resistance Brute taken by the part that is absorbed
    ///     by the bone instead of passing through to organs / wounds.
    /// </summary>
    [DataField]
    public float BruteAbsorbFraction = 0.6f;

    /// <summary>
    ///     Integrity floors: the highest-severity tier whose threshold is
    ///     &gt;= the current integrity wins.
    /// </summary>
    [DataField]
    public Dictionary<FractureSeverity, FixedPoint2> FractureThresholds = new()
    {
        { FractureSeverity.Hairline,   80 },
        { FractureSeverity.Simple,     50 },
        { FractureSeverity.Compound,   25 },
        { FractureSeverity.Comminuted, 5 },
    };

    [DataField]
    public bool BlocksFullHeal = true;

    [DataField, AutoPausedField]
    public TimeSpan NextIntegrityTick;
}
