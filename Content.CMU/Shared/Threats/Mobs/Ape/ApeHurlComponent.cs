using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Ape;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(ApeHurlSystem))]
public sealed partial class ApeHurlComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Debris = "CMUApeDebrisRock";

    [DataField, AutoNetworkedField]
    public float Range = 15;

    [DataField, AutoNetworkedField]
    public float ThrowSpeed = 12;

    /// <summary>
    ///     Upward velocity applied when the target is on a z-level above the ape,
    ///     matching the throw-up velocity of the z-level system.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ZVelocity = 6.5f;
}
