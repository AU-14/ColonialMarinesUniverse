using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The Lodge takes trophy offerings. Put on the Lodge when it is pitched under a Bad Blood rule.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCampOfferingComponent : Component
{
    [DataField]
    public TimeSpan OfferTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Holds the trophies the clan has offered. They spill out when the Lodge is destroyed.
    /// </summary>
    [DataField]
    public string ContainerId = "cmu-bad-blood-offerings";
}

/// <summary>
/// The trophy has been offered at a Lodge and paid for. It cannot be offered again.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodOfferedTrophyComponent : Component;

[Serializable, NetSerializable]
public sealed partial class BadBloodOfferTrophyDoAfterEvent : SimpleDoAfterEvent;
