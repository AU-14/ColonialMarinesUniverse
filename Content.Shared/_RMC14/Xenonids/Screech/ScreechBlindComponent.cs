using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Screech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScreechBlindComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan? EndsAt;

    /// <summary>
    ///     Visible radius in tiles around the player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Radius = 2f;
}
