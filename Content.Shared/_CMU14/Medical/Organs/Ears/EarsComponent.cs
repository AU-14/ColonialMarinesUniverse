using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Ears;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedEarsSystem))]
public sealed partial class EarsComponent : Component
{
    [DataField]
    public bool IsLeftEar = true;

    [DataField, AutoNetworkedField]
    public float HearingMultiplier = 1.0f;
}
