using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Injuries.Shrapnel;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCMUShrapnelSystem))]
public sealed partial class CMUShrapnelComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Fragments;

    [DataField, AutoNetworkedField]
    public float Severity;

    [DataField, AutoNetworkedField]
    public int MaxFragments = 12;
}
