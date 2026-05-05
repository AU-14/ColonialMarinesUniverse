using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Bones;

/// <summary>
///     Temporary post-op window after fracture surgery. A cast stabilizes the
///     limb; without one, the bone can settle into a malunion.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUPostOpBoneSetComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan MalunionCheckAt;

    [DataField, AutoNetworkedField]
    public float MalunionChance = 0.3f;
}
