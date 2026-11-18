using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// A sleeping Bad Blood hound. Only a Bad Blood wakes it, and while a reservation runs only the one it is reserved
/// for. The wake itself is the shared sleeping-hellhound path. CMU14 ARCH-603: the kennel no longer sells sleepers,
/// it is a free spawner (<see cref="BadBloodKennelSpawnerComponent"/>); sleepers are the landing hounds.
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

    /// <summary>
    /// CMU14 ARCH-603: taken from a kennel, so a rebuilt kennel counts it as one of its living hounds.
    /// </summary>
    [ViewVariables]
    public bool FromKennel;

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
