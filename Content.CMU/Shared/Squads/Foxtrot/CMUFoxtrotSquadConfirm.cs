using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Squads.Foxtrot;

/// <summary>
/// Sent from server to client when a late join would take a slot only the Foxtrot squad has left.
/// The client asks for confirmation before the join is retried.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUFoxtrotSquadConfirmEvent(string jobName, string squadName) : EntityEventArgs
{
    public readonly string JobName = jobName;
    public readonly string SquadName = squadName;
}

/// <summary>
/// Sent from client to server to accept joining the Foxtrot squad.
/// </summary>
public sealed class CMUFoxtrotSquadConfirmMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer) { }
    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer) { }
}
