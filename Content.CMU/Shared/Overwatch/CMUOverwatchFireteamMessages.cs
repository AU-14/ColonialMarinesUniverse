using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Overwatch;

/// <summary>
/// Moves a squad member into a fireteam from the overwatch console, or out of every fireteam when
/// <see cref="Fireteam"/> is null.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUOverwatchAssignFireteamBuiMsg(NetEntity marine, int? fireteam) : BoundUserInterfaceMessage
{
    public readonly NetEntity Marine = marine;
    public readonly int? Fireteam = fireteam;
}

/// <summary>
/// Makes a fireteam member its leader from the overwatch console, or stands them down.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUOverwatchFireteamLeaderBuiMsg(NetEntity marine, bool lead) : BoundUserInterfaceMessage
{
    public readonly NetEntity Marine = marine;
    public readonly bool Lead = lead;
}
