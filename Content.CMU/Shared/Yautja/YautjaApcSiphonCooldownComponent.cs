using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaApcSiphonCooldownComponent : Component
{
    [DataField]
    public TimeSpan SiphonAvailableAt;
}
