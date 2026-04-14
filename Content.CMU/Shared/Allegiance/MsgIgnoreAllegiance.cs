using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Allegiance;

/// <summary>
/// Sent from client to server to indicate whether the player wants to ignore allegiance for spawning.
/// </summary>
public sealed class MsgIgnoreAllegiance : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool IgnoreAllegiance;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        IgnoreAllegiance = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(IgnoreAllegiance);
    }
}

