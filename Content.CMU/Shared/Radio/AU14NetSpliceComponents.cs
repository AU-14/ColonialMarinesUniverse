using Content.Shared.DoAfter;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

/// <summary>
///     A fixed mast or array whose feeder junction the CLF can splice a tap into, putting the cell nets on
///     it at its own range and on its own power. All of the per-target balance lives here, so a field mast
///     and a site array can be very different jobs.
/// </summary>
[RegisterComponent]
public sealed partial class AU14NetSpliceTargetComponent : Component
{
    /// <summary>Nets grafted onto the anchor when a splice lands. Removed again when the tap comes out.</summary>
    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new()
    {
        "radioCLF",
        "radioCLFCommand",
    };

    /// <summary>How many trunk carriers have to be found and locked before the tap takes.</summary>
    [DataField]
    public int Carriers = 3;

    /// <summary>Width of the band the carriers hide in. Positions run 1..BandSize.</summary>
    [DataField]
    public int BandSize = 100;

    /// <summary>
    ///     How far either side of a carrier still reads as signal. This sets the opening sweep: with a
    ///     100-wide band and a 30 radius, probes at 15/45/75 always touch a carrier.
    /// </summary>
    [DataField]
    public int CarrierRadius = 30;

    /// <summary>How close a lock has to be to take.</summary>
    [DataField]
    public int LockTolerance = 4;

    /// <summary>Random slop on every strength reading, so the meter cannot be read off as an exact distance.</summary>
    [DataField]
    public int ReadingNoise = 6;

    /// <summary>Total probes for the whole job, shared across every carrier.</summary>
    [DataField]
    public int Probes = 32;

    /// <summary>
    ///     Detection added per probe. Kept low on purpose: searching the band carefully should not be the
    ///     thing that gets the operator caught. Guessing at a lock is.
    /// </summary>
    [DataField]
    public float ProbeDetection = 1.5f;

    /// <summary>Detection added by a lock attempt that misses, plus <see cref="FailedLockProbeCost"/> probes.</summary>
    [DataField]
    public float FailedLockDetection = 30f;

    [DataField]
    public int FailedLockProbeCost = 2;

    /// <summary>How long forcing the junction cover takes before the set can be worked at all.</summary>
    [DataField]
    public float OpenTime = 8f;

    /// <summary>How far the splicer can get from the junction before the job breaks off.</summary>
    [DataField]
    public float WorkRange = 1.5f;
}

/// <summary>
///     Live state of one splice attempt, sitting on the mast being worked. It holds the hidden carrier
///     positions, so it is server-only and never networked: the client is not supposed to know where they
///     are. The mast throws sparks for as long as this is on it, which is the window a passing marine has
///     to notice the job.
/// </summary>
[RegisterComponent]
public sealed partial class AU14NetSpliceInProgressComponent : Component
{
    [ViewVariables]
    public EntityUid User;

    [ViewVariables]
    public EntityUid Kit;

    /// <summary>Copied off the kit as it goes into the junction, since the kit is gone by the time the job
    /// finishes.</summary>
    [ViewVariables]
    public EntProtoId TapPrototype = "AU14CLFNetSpliceTap";

    /// <summary>Hidden carrier positions, one per stage.</summary>
    [ViewVariables]
    public List<int> Carriers = new();

    /// <summary>Which carrier is being hunted now. Equals Carriers.Count once the job is done.</summary>
    [ViewVariables]
    public int Stage;

    [ViewVariables]
    public int ProbesLeft;

    [ViewVariables]
    public float Detection;

    /// <summary>Readings taken against the current carrier. Cleared each time one is locked.</summary>
    [ViewVariables]
    public List<AU14NetSpliceReading> Readings = new();

    /// <summary>Carriers already locked, in order, for the readout.</summary>
    [ViewVariables]
    public List<int> Locked = new();

    [ViewVariables]
    public TimeSpan NextSpark;

    /// <summary>Sparks land on a random gap in this range. A fixed metronome reads as a broken animation.</summary>
    [DataField]
    public float SparkIntervalMin = 1.1f;

    [DataField]
    public float SparkIntervalMax = 2.6f;

    [DataField]
    public EntProtoId SparkEffect = "RMCEffectWeldingSparks";
}

/// <summary>
///     A mast that currently has a tap on it. Points at the tap entity so removing it puts the anchor back
///     exactly as it was, and stops a second tap being stacked on the same junction.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AU14NetSplicedComponent : Component
{
    [DataField]
    public EntityUid Tap;

    /// <summary>Exactly the channels this tap added, so removing it takes those and nothing else.</summary>
    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> Grafted = new();
}

/// <summary>
///     The physical tap: a junction box wired onto the mast's feeder, sharing the mast's tile. It is meant
///     to be findable. It is visible, it can be shot, and anyone can pull it off given
///     <see cref="AU14NetSpliceTapComponent.RemoveTime"/> seconds.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AU14NetSpliceTapComponent : Component
{
    /// <summary>The mast this tap is feeding off. Cleared if the mast dies first.</summary>
    [DataField]
    public EntityUid? Target;

    /// <summary>How long pulling the tap off takes.</summary>
    [DataField]
    public float RemoveTime = 8f;

    /// <summary>
    ///     Dropped by the tap when it is pulled off, as the reward for finding it. Live cell crypto, good
    ///     until CLF leadership orders a recrypto, and nothing beyond that.
    /// </summary>
    [DataField]
    public EntProtoId? SalvagedCrypto = "ANPRCFillCardCLF";
}

/// <summary>
///     A mast whose junction alarmed on a failed splice. It sparks and buzzes for a few minutes, so the
///     garrison has something to walk into and read rather than a silent failure.
/// </summary>
[RegisterComponent]
public sealed partial class AU14NetSpliceAlarmedComponent : Component
{
    [DataField]
    public TimeSpan RecoverDelay = TimeSpan.FromSeconds(90);

    /// <summary>Sparks land on a random gap in this range so the fault reads as a fault, not a metronome.</summary>
    [DataField]
    public float SparkIntervalMin = 2f;

    [DataField]
    public float SparkIntervalMax = 6f;

    /// <summary>
    ///     The buzzer runs on its own much slower timer rather than firing with every spark. A junction that
    ///     buzzes every few seconds for the whole recovery window is unbearable to stand near.
    /// </summary>
    [DataField]
    public float SoundIntervalMin = 12f;

    [DataField]
    public float SoundIntervalMax = 22f;

    [DataField]
    public EntProtoId SparkEffect = "RMCEffectWeldingSparks";

    [DataField]
    public SoundSpecifier AlarmSound =
        new SoundPathSpecifier("/Audio/_RMC14/Machines/buzz_two.ogg", AudioParams.Default.WithVolume(-6f));

    [ViewVariables]
    public TimeSpan RecoverAt;

    [ViewVariables]
    public TimeSpan NextSpark;

    [ViewVariables]
    public TimeSpan NextSound;
}

/// <summary>
///     The splice kit. Consumed either way: on a success it stays in the junction as the tap, on a failure
///     it burns out in there.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AU14NetSpliceKitComponent : Component
{
    /// <summary>What gets left bolted to the mast when the job lands.</summary>
    [DataField]
    public EntProtoId TapPrototype = "AU14CLFNetSpliceTap";

    /// <summary>Played at the mast when the cover starts coming off, so the job is audible from the start.</summary>
    [DataField]
    public SoundSpecifier? WorkSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/hydraulics_1.ogg");

    [DataField]
    public SoundSpecifier? SuccessSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/tech_notification.ogg");
}

/// <summary>DoAfter for forcing the junction cover open before the splice can start.</summary>
[Serializable, NetSerializable]
public sealed partial class AU14NetSpliceOpenDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>DoAfter for pulling a tap back off a mast.</summary>
[Serializable, NetSerializable]
public sealed partial class AU14NetSpliceRemoveDoAfterEvent : SimpleDoAfterEvent
{
}
