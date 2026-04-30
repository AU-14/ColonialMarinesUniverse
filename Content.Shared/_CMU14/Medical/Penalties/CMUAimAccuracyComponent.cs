using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Penalties;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUAimAccuracyComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SwayMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public float SpreadMultiplier = 1.0f;
}
