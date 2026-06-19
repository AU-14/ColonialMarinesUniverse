using Content.Shared._CMU14.Medical.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Effects;

/// <summary>
///     <see cref="StartingHpFraction"/> and <see cref="StartingFracture"/>
///     describe the post-reattach state of the limb.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepReattachLimbEffectComponent : Component
{
    [DataField]
    public float StartingHpFraction = 0.25f;

    [DataField]
    public FractureSeverity StartingFracture = FractureSeverity.Compound;
}
