using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Screech;

/// <summary>
///     Added to guns held by a marine affected by queen screech.
///     Applies a scatter penalty while present.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScreechScatterComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle AngleIncrease = Angle.FromDegrees(45);
}
