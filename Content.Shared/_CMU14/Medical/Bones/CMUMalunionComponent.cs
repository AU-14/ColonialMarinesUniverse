using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Bones;

/// <summary>
///     Marks a fracture that healed badly after being left unstabilized.
///     It still uses the normal fracture surgery path, but splints do not
///     suppress it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUMalunionComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppearedAt;
}
