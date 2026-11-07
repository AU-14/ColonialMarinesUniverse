using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CriminalRecords;

/// <summary>A firearm colony logistics bought this round: what it is, its numbers and who ordered it.</summary>
[Serializable, NetSerializable]
public sealed record CMUFirearmRegistryEntry(
    string Serial,
    string Signature,
    string Name,
    EntProtoId? Prototype,
    string Buyer,
    string Order,
    TimeSpan Time);

/// <summary>Sent by a records console's window when it opens, asking for the firearm registry.</summary>
[Serializable, NetSerializable]
public sealed class CMUFirearmRegistryRequestMsg : BoundUserInterfaceMessage;

/// <summary>The whole firearm registry, sent to open records consoles whenever it changes.</summary>
[Serializable, NetSerializable]
public sealed class CMUFirearmRegistryMsg(List<CMUFirearmRegistryEntry> entries) : BoundUserInterfaceMessage
{
    public readonly List<CMUFirearmRegistryEntry> Entries = entries;
}

/// <summary>
///     Sent to a records console's window after a casing, gun or forensic scan was compared against the registry: the
///     firearms page opens filtered to the signature.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUFirearmRegistryCompareMsg(string signature) : BoundUserInterfaceMessage
{
    public readonly string Signature = signature;
}
