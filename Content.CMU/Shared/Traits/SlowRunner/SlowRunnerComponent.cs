using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.SlowRunner;

[RegisterComponent, NetworkedComponent]
public sealed partial class SlowRunnerComponent : Component
{
    [DataField]
    public float SprintSpeedModifier = 0.85f;
}
