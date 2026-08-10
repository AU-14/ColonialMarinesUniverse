using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs.Larva;

[RegisterComponent, NetworkedComponent]
public sealed partial class BloodyLarvaComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(15);

    public TimeSpan RemoveAt;
}