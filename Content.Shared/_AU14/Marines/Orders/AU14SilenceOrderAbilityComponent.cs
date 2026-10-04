using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Marines.Orders;

/// <summary>
/// Added to the GOVFOR Platoon Commander. Grants the Silence Order ability with its own independent cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14SilenceOrderAbilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId SilenceAction = "ActionMarineGovforSilence";

    [DataField, AutoNetworkedField]
    public EntityUid? SilenceActionEntity;

    /// <summary>
    /// Cooldown in seconds between uses. Independent from other marine orders.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long targets remain whisper-only.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Radius of the effect in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 7f;
}
