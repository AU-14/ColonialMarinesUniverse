using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.BugHelp;

/// <summary>Server → client: open the bug report window. Player-bound, no entity.</summary>
public sealed class BugHelpOpenMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}

/// <summary>Client → server: submit the bug report window's fields.</summary>
public sealed class BugHelpSubmitMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public string WhatHappened = string.Empty;
    public string? Expected;
    public string? Steps;
    public string? ItemQuery;
    public string? ExamineText;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        WhatHappened = buffer.ReadString();
        Expected = ReadOptional(buffer);
        Steps = ReadOptional(buffer);
        ItemQuery = ReadOptional(buffer);
        ExamineText = ReadOptional(buffer);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(WhatHappened);
        WriteOptional(buffer, Expected);
        WriteOptional(buffer, Steps);
        WriteOptional(buffer, ItemQuery);
        WriteOptional(buffer, ExamineText);
    }

    private static string? ReadOptional(NetIncomingMessage buffer)
    {
        return buffer.ReadBoolean() ? buffer.ReadString() : null;
    }

    private static void WriteOptional(NetOutgoingMessage buffer, string? value)
    {
        buffer.Write(value != null);
        if (value != null)
            buffer.Write(value);
    }
}
