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
public sealed class AU14CallsignConsoleElement(NetEntity? squad, string? group, string? category, string label, string word, List<AU14CallsignConsoleRow> rows)
{
    public readonly NetEntity? Squad = squad;

    // set when this element is a custom callsign group created from the console
    public readonly string? Group = group;

    // set when this element is a fixed role section (AIR, MP, MEDICAL, INTEL)
    public readonly string? Category = category;

    public readonly string Label = label;

    public readonly string Word = word;

    public readonly List<AU14CallsignConsoleRow> Rows = rows;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignConsoleState(string faction, List<AU14CallsignConsoleElement> elements, List<string> groups)
    : BoundUserInterfaceState
{
    public readonly string Faction = faction;
    public readonly List<AU14CallsignConsoleElement> Elements = elements;
    public readonly List<string> Groups = groups;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignRenameElementMsg(NetEntity? squad, string? category, string word) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Squad = squad;
    public readonly string? Category = category;
    public readonly string Word = word;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignSetSuffixMsg(NetEntity member, string suffix) : BoundUserInterfaceMessage
{
    public readonly NetEntity Member = member;
    public readonly string Suffix = suffix;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignCreateGroupMsg(string word) : BoundUserInterfaceMessage
{
    public readonly string Word = word;
}

[Serializable, NetSerializable]
public sealed class AU14CallsignDeleteGroupMsg(string word) : BoundUserInterfaceMessage
{
    public readonly string Word = word;
}

// group = null moves the member back to their automatic squad/command element
[Serializable, NetSerializable]
public sealed class AU14CallsignAssignGroupMsg(NetEntity member, string? group) : BoundUserInterfaceMessage
{
    public readonly NetEntity Member = member;
    public readonly string? Group = group;
}

// sent through the overwatch console UI on entities that also carry the
// directory, pops the comms net directory window for the operator
[Serializable, NetSerializable]
public sealed class AU14CallsignOpenDirectoryMsg : BoundUserInterfaceMessage;

public static class AU14Callsigns
{
    public const int MaxWordLength = 10;
    public const int MaxSuffixLength = 8;
}
