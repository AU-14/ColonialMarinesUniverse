using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Xenomorphs.Swarm;

[Serializable, NetSerializable]
public enum CMUSwarmUiKey : byte { Key }

[Serializable, NetSerializable]
public enum CMUSwarmOrderKind : byte { Idle, Guard, Follow, Roam, Patrol, Build, Weed, Rally, Attack, Defend }

[Serializable, NetSerializable]
public enum CMUSwarmAggression : byte { Passive, Defensive, Aggressive }

[Serializable, NetSerializable]
public enum CMUSwarmStructure : byte { Wall, Membrane, Door }

[Serializable, NetSerializable]
public enum CMUSwarmCommand : byte { Prepare, AssignSquad, Aggression, Release, ClearMarkers }

[Serializable, NetSerializable]
public sealed record CMUSwarmMemberInfo(NetEntity Entity, int Number, int Squad,
    CMUSwarmOrderKind Order, CMUSwarmAggression Aggression, bool Resting, bool Working);

[Serializable, NetSerializable]
public sealed class CMUSwarmUiState(List<CMUSwarmMemberInfo> members, int cooldownSeconds) : BoundUserInterfaceState
{
    public readonly List<CMUSwarmMemberInfo> Members = members;
    public readonly int CooldownSeconds = cooldownSeconds;
}

[Serializable, NetSerializable]
public sealed class CMUSwarmCommandMessage(CMUSwarmCommand command, List<NetEntity> members,
    int squad = 0, CMUSwarmOrderKind order = CMUSwarmOrderKind.Guard,
    CMUSwarmAggression aggression = CMUSwarmAggression.Defensive,
    CMUSwarmStructure structure = CMUSwarmStructure.Wall, bool append = false) : BoundUserInterfaceMessage
{
    public readonly CMUSwarmCommand Command = command;
    public readonly List<NetEntity> Members = members;
    public readonly int Squad = squad;
    public readonly CMUSwarmOrderKind Order = order;
    public readonly CMUSwarmAggression Aggression = aggression;
    public readonly CMUSwarmStructure Structure = structure;
    public readonly bool Append = append;
}

public sealed partial class CMUPlaceSwarmOrderActionEvent : Content.Shared.Actions.WorldTargetActionEvent;

/// <summary>No sprite: only hive xenos' overlay renders these tactical symbols.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSwarmOrderFlagComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid? Hive;
    [DataField, AutoNetworkedField] public CMUSwarmOrderKind Kind;
    [DataField, AutoNetworkedField] public int Number;
}
