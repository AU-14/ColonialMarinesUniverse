using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery.Conditions;

/// <summary>
///     Set <see cref="RequireSeverity"/> for an exact match or
///     <see cref="RequireAtLeast"/> and <see cref="RequireAtMost"/> for a range.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUFracturedSurgeryConditionComponent : Component
{
    [DataField]
    public FractureSeverity? RequireSeverity;

    [DataField]
    public FractureSeverity? RequireAtLeast;

    [DataField]
    public FractureSeverity? RequireAtMost;
}
