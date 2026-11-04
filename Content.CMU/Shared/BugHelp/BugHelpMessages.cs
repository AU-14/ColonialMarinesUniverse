using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.BugHelp;

/// <summary>
/// Which kind of bug a report describes. Grows by adding a member plus a
/// cmu-bughelp-category-{name} Fluent line; both sides pick the rest up automatically.
/// </summary>
public enum BugHelpCategory
{
    Code = 0,
    Mapping = 1,
}

public static class BugHelpAnonymity
{
    /// <summary>Alias for the mentee on every Discord surface: relay lines, transcripts, promoted reports.</summary>
    public const string Alias = "User";
}

/// <summary>Server → client: whether bug help is enabled. Sent on connect and when config changes.</summary>
public sealed class BugHelpConfigMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public bool Enabled;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Enabled = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Enabled);
    }
}

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

/// <summary>Client → server: staff promotes a mentor conversation into a bug report (§4.2).</summary>
public sealed class BugHelpPromoteMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public Guid Destination;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Destination = buffer.ReadGuid();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Destination);
    }
}

/// <summary>Client → server: submit the bug report window's fields.</summary>
public sealed class BugHelpSubmitMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Core;

    public BugHelpCategory Category = BugHelpCategory.Code;
    public string WhatHappened = string.Empty;
    public string? Expected;
    public string? Steps;
    public string? ItemQuery;
    public string? ExamineText;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Category = (BugHelpCategory) buffer.ReadByte();
        WhatHappened = buffer.ReadString();
        Expected = ReadOptional(buffer);
        Steps = ReadOptional(buffer);
        ItemQuery = ReadOptional(buffer);
        ExamineText = ReadOptional(buffer);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write((byte) Category);
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
