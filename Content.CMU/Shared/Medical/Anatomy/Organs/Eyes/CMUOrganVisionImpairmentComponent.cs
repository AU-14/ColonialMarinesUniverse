using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;

/// <summary>
/// Persistent organ blur, independent of temporary eye damage and its healing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedEyesSystem))]
public sealed partial class CMUOrganVisionImpairmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Magnitude;
}
