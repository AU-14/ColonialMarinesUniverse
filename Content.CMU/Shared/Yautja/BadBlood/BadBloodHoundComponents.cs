using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The Lodge kennel: any Bad Blood buys a sleeping hound here.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodKennelComponent : Component
{
    /// <summary>
    /// How long only the buyer can wake a hound they paid for.
    /// </summary>
    [DataField]
    public TimeSpan BuyerReservation = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Handed to the buyer with each hound, so they can watch through it.
    /// </summary>
    [DataField]
    public EntProtoId? PadPrototype = "CMUBadBloodHoundObservationPad";
}

/// <summary>
/// A sleeping Bad Blood hound. Only a Bad Blood wakes it, and for a while after a purchase only the buyer.
/// The wake itself is the shared sleeping-hellhound path.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodSleepingHoundComponent : Component
{
    [ViewVariables]
    public EntityUid? ReservedFor;

    [ViewVariables]
    public TimeSpan ReservedUntil;
}

/// <summary>
/// A woken Bad Blood hound. It stands idle until a ghost takes it, and goes back to sleep if nobody does.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodHoundComponent : Component
{
    [ViewVariables]
    public TimeSpan WokenAt;

    /// <summary>
    /// A player has driven this hound. It no longer goes back to sleep.
    /// </summary>
    [ViewVariables]
    public bool Claimed;

    [DataField]
    public TimeSpan UnclaimedSleepAfter = TimeSpan.FromMinutes(3);

    /// <summary>
    /// While the Blood Reserve has anyone waiting, only they can ask for the hound for this long.
    /// </summary>
    [DataField]
    public TimeSpan ReservePriority = TimeSpan.FromSeconds(60);

    [DataField]
    public EntProtoId SleeperPrototype = "CMUBadBloodSleepingHound";
}
