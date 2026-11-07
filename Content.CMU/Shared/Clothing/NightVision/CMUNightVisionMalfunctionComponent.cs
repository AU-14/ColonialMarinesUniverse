using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Clothing.NightVision;

/// <summary>
///     Unreliable night vision goggles. While they're switched on and giving someone night vision, the wearer gives
///     off a faint glow others can spot, and every so often the goggles may cut out on their own. Once they have, they
///     won't switch back on until someone fixes them with a screwdriver.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUNightVisionMalfunctionSystem))]
public sealed partial class CMUNightVisionMalfunctionComponent : Component
{
    /// <summary>How often, while worn and switched on, the goggles roll to see if they cut out.</summary>
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    /// <summary>The chance, from 0 to 1, that each roll makes the goggles cut out.</summary>
    [DataField]
    public float MalfunctionChance = 0.1f;

    /// <summary>When the next roll happens. Only counts down while the goggles are worn and switched on.</summary>
    [DataField, AutoPausedField]
    public TimeSpan NextCheck;

    /// <summary>Whether the goggles have cut out and need repairing before they'll switch on again.</summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>The tool quality needed to repair the goggles.</summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Screwing";

    /// <summary>How long the repair takes.</summary>
    [DataField]
    public TimeSpan RepairDelay = TimeSpan.FromSeconds(3);

    /// <summary>The light entity attached to the wearer while the goggles are glowing.</summary>
    [DataField]
    public EntProtoId GlowPrototype = "CMUNightVisionGlow";

    /// <summary>The glow currently attached to the wearer, if any. Only the server tracks this.</summary>
    [ViewVariables]
    public EntityUid? Glow;
}

[Serializable, NetSerializable]
public sealed partial class CMUNightVisionRepairDoAfterEvent : SimpleDoAfterEvent;
