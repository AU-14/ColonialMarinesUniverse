using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.Jamming;

[Serializable, NetSerializable]
public enum CMUGunJamType : byte
{
    None,
    FailureToFeed,
    FailureToEject,
    FailureToChamber,
    FailureToFire,
}

/// <summary>
///     What happens to the stuck round when a jam is cycled clear.
/// </summary>
public enum CMUGunJamRoundLoss : byte
{
    /// <summary>The round stays in the gun.</summary>
    None,

    /// <summary>The live round drops on the floor.</summary>
    Eject,

    /// <summary>The round is lost.</summary>
    Destroy,
}
