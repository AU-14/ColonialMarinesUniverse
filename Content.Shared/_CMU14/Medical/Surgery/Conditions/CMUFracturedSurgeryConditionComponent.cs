using Content.Shared._CMU14.Medical.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery.Conditions;

/// <summary>
///     Set <see cref="RequireSeverity"/> for an exact match or
///     <see cref="RequireAtLeast"/> for a severity floor.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSurgerySystem))]
public sealed partial class CMUFracturedSurgeryConditionComponent : Component
{
    [DataField]
    public FractureSeverity? RequireSeverity;

    [DataField]
    public FractureSeverity? RequireAtLeast;
}
