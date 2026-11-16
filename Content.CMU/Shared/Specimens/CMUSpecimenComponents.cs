using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Specimens;

/// <summary>
///     A projectile that builds up sedation on the xenonid it hits. Enough hits in a short time
///     put the xenonid under for <see cref="SedationTime"/>; further hits keep it under.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUSpecimenCaptureSystem))]
public sealed partial class CMUSedateOnHitComponent : Component
{
    [DataField]
    public float Amount = 1f;

    /// <summary>
    ///     Paralysis applied when the target goes under, before RMC's xeno stun reduction.
    /// </summary>
    [DataField]
    public TimeSpan SedationTime = TimeSpan.FromSeconds(30);
}

/// <summary>
///     Sedation built up on a specimen. Decays over time. Once it reaches the specimen's threshold
///     the specimen is held down until <see cref="SedatedUntil"/>; the clock stops while it's crated.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUSpecimenCaptureSystem))]
public sealed partial class CMUSedationComponent : Component
{
    [DataField]
    public float Level;

    [DataField]
    public TimeSpan LastUpdate;

    /// <summary>
    ///     When the sedation wears off. Null while not sedated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? SedatedUntil;

    [DataField]
    public TimeSpan NextHold;

    /// <summary>
    ///     The specimen's own grab-paralysis, suspended while it's sedated so handlers can drag it.
    ///     Null if it had none.
    /// </summary>
    [DataField]
    public TimeSpan? SuspendedPullParalysis;
}

[Serializable, NetSerializable]
public enum CMUSpecimenCrateState : byte
{
    Closed,
    Opening,
    Open,
    Closing,
}

[Serializable, NetSerializable]
public enum CMUSpecimenCrateLayers : byte
{
    Base,
}

/// <summary>
///     A secure crate that holds one specimen and can be pulled. Drag a downed specimen onto the
///     open crate and close the lid to seal it in; opening the lid lets it out. Opening needs the
///     crate's access.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(CMUSpecimenCaptureSystem))]
public sealed partial class CMUSpecimenCrateComponent : Component
{
    [DataField]
    public string ContainerId = "cmu_specimen";

    [DataField, AutoNetworkedField]
    public CMUSpecimenCrateState State = CMUSpecimenCrateState.Open;

    /// <summary>
    ///     When the lid finishes moving, while opening or closing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? MovingUntil;

    /// <summary>
    ///     How long the lid takes, matching the sprite's four 0.365s frames.
    /// </summary>
    [DataField]
    public TimeSpan MoveTime = TimeSpan.FromSeconds(1.46);

    [DataField]
    public SoundSpecifier? MoveSound = new SoundPathSpecifier("/Audio/_RMC14/Structures/secure_box_opening/secure_box_opening.ogg");

    /// <summary>
    ///     How long it takes to force a downed, cuffed or unconscious person into the crate.
    /// </summary>
    [DataField]
    public TimeSpan PutInTimeRestrained = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     How long it takes to force in someone who's still up and free.
    /// </summary>
    [DataField]
    public TimeSpan PutInTime = TimeSpan.FromSeconds(8);
}

/// <summary>
///     Forcing a person into a specimen crate.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUCratePutInDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent
{
}

/// <summary>
///     A person shut in a specimen crate, muffled for as long as they're in it. Remembers whether
///     the crate gave them the muffle, so letting them out doesn't strip one they already had.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCratedPersonComponent : Component
{
    [DataField]
    public bool AddedMuffle;
}
