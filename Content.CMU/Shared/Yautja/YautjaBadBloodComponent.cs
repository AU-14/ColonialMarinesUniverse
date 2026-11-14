using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaBadBloodComponent : Component
{
    // not networked: only spaces out the "your hands reject it" popup while a trigger is held
    [ViewVariables]
    public TimeSpan NextWeaponRefusePopup;
}
