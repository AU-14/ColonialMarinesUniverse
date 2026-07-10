using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Callsigns;

[Serializable, NetSerializable]
public enum AU14CallsignConsoleUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AU14CallsignConsoleRow(NetEntity member, string callsign, string name, string job)
{
    public readonly NetEntity Member = member;
    public readonly string Callsign = callsign;
    public readonly string Name = name;
    public readonly string Job = job;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignConsoleElement(NetEntity? squad, string label, string word, List<AU14CallsignConsoleRow> rows)
{
    public readonly NetEntity? Squad = squad;

    public readonly string Label = label;

    public readonly string Word = word;

    public readonly List<AU14CallsignConsoleRow> Rows = rows;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignConsoleState(string faction, List<AU14CallsignConsoleElement> elements)
    : BoundUserInterfaceState
{
    public readonly string Faction = faction;
    public readonly List<AU14CallsignConsoleElement> Elements = elements;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignRenameElementMsg(NetEntity? squad, string word) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Squad = squad;
    public readonly string Word = word;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignSetSuffixMsg(NetEntity member, string suffix) : BoundUserInterfaceMessage
{
    public readonly NetEntity Member = member;
    public readonly string Suffix = suffix;
}

public static class AU14Callsigns
{
    public const int MaxWordLength = 10;
    public const int MaxSuffixLength = 8;
}
