using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Marines.Orders;

/// <summary>
/// Added to entities under the Silence Order effect. Forces whisper-only speech for the duration.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14SilenceOrderComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
