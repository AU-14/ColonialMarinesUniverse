using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Mentor;

/// <summary>Client → server: the mhelp window opened; how many mentors can see a message sent now?</summary>
public sealed class CMUMentorOnlineCountRequestMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer) { }
    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer) { }
}

/// <summary>Server → client: mentors connected and receiving mentor helps, stealthed admins excluded.</summary>
public sealed class CMUMentorOnlineCountMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public int Count;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        => Count = buffer.ReadInt32();

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        => buffer.Write(Count);
}
