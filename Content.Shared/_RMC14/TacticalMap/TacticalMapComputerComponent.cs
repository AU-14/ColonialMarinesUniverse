using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapComputerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Map;

    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> Blips = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastAnnounceAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextAnnounceAt;

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillLeadership";

    [DataField, AutoNetworkedField]
    public int SkillLevel = 2;

    [DataField("faction"), AutoNetworkedField]
    public string? Faction;

    /// <summary>Squad blips for overwatch consoles assigned to a squad.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<int, TacticalMapBlip> SquadBlips = new();

    /// <summary>Squad canvas lines for overwatch consoles assigned to a squad.</summary>
    [DataField, AutoNetworkedField]
    public List<TacticalMapLine> SquadLines = new();

    /// <summary>Squad tactical labels for overwatch consoles assigned to a squad.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, string> SquadLabels = new();
}
