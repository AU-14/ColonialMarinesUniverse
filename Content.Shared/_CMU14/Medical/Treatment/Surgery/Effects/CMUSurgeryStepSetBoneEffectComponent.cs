using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery.Effects;

/// <summary>
///     If the part's <see cref="FractureComponent.Severity"/> matches
///     <see cref="DowngradeFrom"/>, set it to <see cref="DowngradeTo"/> and
///     restore <see cref="BoneComponent.Integrity"/> to
///     <see cref="IntegrityRestore"/>. The fracture is removed entirely when
///     <see cref="DowngradeTo"/> is <see cref="FractureSeverity.None"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepSetBoneEffectComponent : Component
{
    [DataField]
    public FractureSeverity DowngradeFrom = FractureSeverity.Simple;

    /// <summary>
    ///     Optional set of accepted starting severities. When populated it
    ///     takes precedence over <see cref="DowngradeFrom"/>.
    /// </summary>
    [DataField]
    public HashSet<FractureSeverity> DowngradeFromAnyOf = new();

    [DataField]
    public FractureSeverity DowngradeTo = FractureSeverity.None;

    [DataField]
    public FixedPoint2 IntegrityRestore = 70;
}
