using Robust.Shared.Network;

namespace Content.Shared._CMU14.Xenonids.CorruptedHive;

/// <summary>
/// Marks an intact xenonid egg as a valid target for the corrupted ciphering reagent.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCipherableXenoEggComponent : Component
{
    [DataField]
    public bool Converted;
}

/// <summary>
/// Applies the corrupted-hive claim rules to a facehugger created by ciphering an egg.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCorruptedParasiteComponent : Component
{
    public NetUserId? ReservedFor;

    public TimeSpan? ReservationExpiresAt;

    public uint OfferId;
}
