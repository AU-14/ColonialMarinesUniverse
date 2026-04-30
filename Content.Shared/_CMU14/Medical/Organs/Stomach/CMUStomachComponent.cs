using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Stomach;

/// <summary>
///     CMU-prefixed to avoid clashing with vanilla SS14's <c>StomachComponent</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedStomachSystem))]
public sealed partial class CMUStomachComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DigestionMultiplier = 1.0f;

    [DataField, AutoPausedField]
    public TimeSpan NextVomitCheck;
}
