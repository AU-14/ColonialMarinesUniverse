using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Players.JobWhitelist;

public sealed class MsgJobWhitelist : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public HashSet<string> Whitelist = new();
    public Content.Shared._CMU14.Yautja.YautjaProfileCapabilities YautjaCapabilities =
        Content.Shared._CMU14.Yautja.YautjaProfileCapabilities.Default;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        Whitelist.EnsureCapacity(count);

        for (var i = 0; i < count; i++)
        {
            Whitelist.Add(buffer.ReadString());
        }

        var length = buffer.ReadVariableInt32();
        using var stream = new System.IO.MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out YautjaCapabilities);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Whitelist.Count);

        foreach (var ban in Whitelist)
        {
            buffer.Write(ban);
        }

        using var stream = new System.IO.MemoryStream();
        serializer.SerializeDirect(stream, YautjaCapabilities);
        buffer.WriteVariableInt32((int) stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
