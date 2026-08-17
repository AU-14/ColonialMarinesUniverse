using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;

/// <summary>
/// Added to a xeno that has become the Pathogen Overmind.
/// Marks them as the hive queen and tracks their linked blight core.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUXenoOvermindComponent : Component
{
    /// <summary>The blight core this Overmind is linked to.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedCore;

    /// <summary>
    /// How long after round start the Overmind must wait before
    /// gaining enhanced cross-map heal abilities.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StrengthensAfter = TimeSpan.FromMinutes(10);

    [DataField, AutoNetworkedField]
    public bool Strengthened;

    [DataField, AutoNetworkedField]
    public EntityUid? Eye;
}
