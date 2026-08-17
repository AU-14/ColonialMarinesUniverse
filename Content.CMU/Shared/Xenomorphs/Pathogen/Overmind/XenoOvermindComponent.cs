using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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

    /// <summary>
    /// Actions granted only in eye (incorporeal) form.
    /// Granted on eye entry, removed on exit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> EyeFormActions = new()
    {
        "CMU14ActionXenoPathogenHeal",
        "CMU14ActionXenoPathogenPheromones",
        "CMU14ActionXenoPathogenWatch",
        "CMU14ActionXenoPathogenRest",
        "CMU14ActionXenoPathogenExpandWeeds",
    };

    /// <summary>Tracks spawned action entities for eye form so we can remove them.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> EyeFormActionEntities = new();

    /// <summary>
    /// Actions granted only in physical form.
    /// Granted on physical entry, removed on exit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> PhysicalFormActions = new()
    {
        "CMU14ActionXenoPathogenParalyzingSlash",
        "CMU14ActionXenoPathogenBlightWave",
        "CMU14ActionXenoPathogenWordQueen",
    };

    /// <summary>Tracks spawned action entities for physical form so we can remove them.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> PhysicalFormActionEntities = new();

    [DataField, AutoNetworkedField]
    public EntProtoId EyeFormActionOrderId = "CMU14XenoOvermindEyeOrder";

    [DataField, AutoNetworkedField]
    public EntProtoId PhysicalFormActionOrderId = "CMU14XenoOvermindPhysicalOrder";
}