using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class AnemicComponent : Component
{
    [DataField]
    public float BleedRateMultiplier = 1.2f;

    [DataField]
    public TimeSpan WarnCooldown = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan NextWarnMessage;
}
