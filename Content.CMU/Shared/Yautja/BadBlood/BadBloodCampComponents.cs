using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The Lodge core. One stands at a time, and the clan's rule entity points at it.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCampComponent : Component
{
    [ViewVariables]
    public TimeSpan LastDamagedAt;

    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// The Lodge mends itself once it has gone this long without taking damage.
    /// </summary>
    [DataField]
    public TimeSpan RegenDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Health mended per second.
    /// </summary>
    [DataField]
    public float Regen = 1f;

    [ViewVariables]
    public TimeSpan? LastAlertAt;

    [DataField]
    public TimeSpan AlertCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long the Lodge shows on the marine map after each hit.
    /// </summary>
    [DataField]
    public TimeSpan AttackRevealTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timed reveals run until this time.
    /// </summary>
    [ViewVariables]
    public TimeSpan RevealedUntil;

    [ViewVariables]
    public bool RevealPermanent;

    [ViewVariables]
    public bool Revealed;

    /// <summary>
    /// Added while the Lodge is revealed to marines and removed when it is hidden again.
    /// </summary>
    [DataField]
    public ComponentRegistry RevealComponents = new();

    /// <summary>
    /// The destruction has been handled, so it is not handled again when the entity is deleted.
    /// </summary>
    [ViewVariables]
    public bool Destroyed;
}

/// <summary>
/// The Lodge kit the Leader gets from Pitch Camp. Only the Leader can deploy it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodCampKitComponent : Component;

[Serializable, NetSerializable]
public enum BadBloodCampPartKind : byte
{
    Spit,
    AscensionStone,
    Armory,
    Kennel,
    Cauldron,
}

/// <summary>
/// A Lodge part. It is deployed with the Lodge and goes when the Lodge goes.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCampPartComponent : Component
{
    [DataField(required: true)]
    public BadBloodCampPartKind Kind;
}

/// <summary>
/// Raised on a Lodge kit, on the server, before a deploy starts and every tick while it runs. The user is already
/// known to be the Leader. Cancel it to refuse the spot.
/// </summary>
[ByRefEvent]
public record struct BadBloodCampKitDeployAttemptEvent(EntityUid User, bool Quiet)
{
    public bool Cancelled;
}
