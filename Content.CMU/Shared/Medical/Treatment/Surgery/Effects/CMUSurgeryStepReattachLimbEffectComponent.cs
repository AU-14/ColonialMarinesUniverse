using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery.Effects;

/// <summary>
///     <see cref="StartingHpFraction"/> and <see cref="StartingFracture"/>
///     describe the post-reattach state of the limb.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUSurgeryStepReattachLimbEffectComponent : Component
{
    /// <summary>Optional procedure override; absent uses the server's reattachment HP CVar.</summary>
    [DataField]
    public float? StartingHpFraction;

    [DataField]
    public FractureSeverity StartingFracture = FractureSeverity.Compound;
}
