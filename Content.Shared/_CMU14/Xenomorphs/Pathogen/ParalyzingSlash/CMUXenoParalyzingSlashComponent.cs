using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenonids.ParalyzingSlash;

/// <summary>
/// Marks a xeno as having the Pathogen Paralyzing Slash armed and ready.
/// Consumed on the next successful melee hit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUXenoParalyzingSlashPendingComponent : Component
{
    /// <summary>
    /// How long the slow lasts on the target when applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Whether to apply super slowdown instead of regular slowdown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SuperSlow;

}