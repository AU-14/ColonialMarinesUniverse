using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedEyesSystem))]
public sealed partial class EyesComponent : Component
{
    [DataField]
    public bool IsLeftEye = true;
}

/// <summary>
///     Marks blindness caused by dead or absent eyes. This is kept separate
///     from temporary blindness so restoring an eye does not clear unrelated
///     blindness sources such as flashes or neurotoxin.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedEyesSystem))]
public sealed partial class CMUOrganBlindnessComponent : Component;
