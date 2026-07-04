using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Injuries.Shrapnel;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUProjectileShrapnelComponent : Component
{
    [DataField]
    public int Fragments = 1;

    [DataField]
    public float Severity = 10f;
}
