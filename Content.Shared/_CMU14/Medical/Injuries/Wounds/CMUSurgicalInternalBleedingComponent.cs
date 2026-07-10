using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Persistent internal-bleeding source left when an incision is closed
///     before its surgical bleeders are clamped.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCMUWoundsSystem))]
public sealed partial class CMUSurgicalInternalBleedingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BloodlossPerSecond = 0.5f;
}
