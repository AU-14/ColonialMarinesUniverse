using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Kidneys;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedKidneysSystem))]
public sealed partial class KidneysComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WasteFiltration = 1.0f;

    [DataField]
    public bool IsLeftKidney = true;
}
