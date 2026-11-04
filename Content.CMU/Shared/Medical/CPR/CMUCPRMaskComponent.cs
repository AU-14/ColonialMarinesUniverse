using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.CPR;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCPRMaskComponent : Component
{
    [DataField]
    public float RevivableTimeBonus = 1f / 3f;
}
