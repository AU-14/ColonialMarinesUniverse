using Content.Shared._RMC14.Marines.Squads;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

[Serializable, NetSerializable]
public enum SquadLeaderTrackerUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly Dictionary<SquadObjectiveType, string> Objectives;

    // CMU14: callsigns of the squad, filled server-side for the viewer's faction only
    public readonly Dictionary<NetEntity, string> Callsigns = new();

    public SquadLeaderTrackerBoundUserInterfaceState(Dictionary<SquadObjectiveType, string> objectives)
    {
        Objectives = objectives;
    }

    // CMU14 method
    public SquadLeaderTrackerBoundUserInterfaceState(Dictionary<SquadObjectiveType, string> objectives,
        Dictionary<NetEntity, string> callsigns) : this(objectives)
    {
        Callsigns = callsigns;
    }
}

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerAssignFireteamMsg(NetEntity marine, int fireteam) : BoundUserInterfaceMessage
{
    public readonly NetEntity Marine = marine;
    public readonly int Fireteam = fireteam;
}

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerUnassignFireteamMsg(NetEntity marine) : BoundUserInterfaceMessage
{
    public readonly NetEntity Marine = marine;
}

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerPromoteFireteamLeaderMsg(NetEntity marine) : BoundUserInterfaceMessage
{
    public readonly NetEntity Marine = marine;
}

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerDemoteFireteamLeaderMsg(int fireteam) : BoundUserInterfaceMessage
{
    public readonly int Fireteam = fireteam;
}

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerChangeTrackedMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerSetFireteamNicknameMsg(int index, string nickname) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly string Nickname = nickname;
}
