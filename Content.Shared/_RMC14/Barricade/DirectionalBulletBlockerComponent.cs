using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Barricade;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DirectionalBulletBlockerComponent : Component
{
    /// <summary>
    /// Degrees cone in front of the barricade where bullets are blocked
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FrontBlockAngle = 135f;

    /// <summary>
    /// Chance to block a bullet when it's in the blocking cone (0.0 to 1.0)
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlockChance = 1.0f;

}
