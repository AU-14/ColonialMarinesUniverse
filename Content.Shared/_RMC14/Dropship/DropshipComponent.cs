using Content.Shared.Doors.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class DropshipComponent : Component
{
    [DataField, AutoNetworkedField]
    public FTLState State;

    [DataField]
    public EntityUid? Destination;

    [DataField]
    public EntityUid? DepartureLocation;

    [DataField, AutoNetworkedField]
    public bool Crashed;

    [DataField, AutoNetworkedField]
    public SoundSpecifier LocalHijackSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/Shuttle/queen_alarm.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier MarineHijackSound = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/hijack.ogg", AudioParams.Default.WithVolume(-5));

    [DataField, AutoNetworkedField]
    public SoundSpecifier GeneralQuartersSound = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/GQfullcall.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier UnidentifledlifesignsSound = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/unidentified_lifesigns.ogg");

    [DataField, AutoNetworkedField]
    public TimeSpan LockCooldown = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public Dictionary<DoorLocation, TimeSpan> LastLocked = new ();

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> AttachmentPoints = new();

    [DataField, AutoNetworkedField]
    public TimeSpan? RechargeTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? HijackLandAt;

    [DataField, AutoNetworkedField]
    public EntProtoId FireId = "RMCTileFire";

    [DataField, AutoNetworkedField]
    public int FireRange = 11;

    [DataField, AutoNetworkedField]
    public SoundSpecifier CrashWarningSound = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/dropship_emergency.ogg", AudioParams.Default.WithVolume(-5));

    [DataField, AutoNetworkedField]
    public SoundSpecifier CrashSound = new SoundPathSpecifier("/Audio/_RMC14/Dropship/dropship_crash.ogg", AudioParams.Default.WithVolume(-1));

    [DataField, AutoNetworkedField]
    public SoundSpecifier IncomingSound = new SoundPathSpecifier("/Audio/_RMC14/Dropship/dropship_incoming.ogg", AudioParams.Default.WithVolume(-1));

    [DataField, AutoNetworkedField]
    public bool AnnouncedCrash;

    [DataField, AutoNetworkedField]
    public bool DidIncomingSound;

    [DataField, AutoNetworkedField]
    public bool DidExplosion;

    [DataField, AutoNetworkedField]
    public TimeSpan AnnounceCrashTime = TimeSpan.FromSeconds(23);

    [DataField, AutoNetworkedField]
    public TimeSpan PlayIncomingSoundTime = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public TimeSpan ExplodeTime = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan CancelFlightTime = TimeSpan.FromSeconds(10);

    [DataField]
    public EntityUid? LaunchAlarmEntity;

    // cmu edit start: cmss13-devs/cmss13-pve#1294 dropship flight sounds
    /// <summary>Optional takeoff cue; null preserves the standard shuttle startup sound.</summary>
    [DataField]
    public SoundSpecifier? StartupSound = new SoundPathSpecifier("/Audio/CMU14/Dropship/dropship_flight_start.ogg");

    /// <summary>Takeoff cue when the dropship leaves a warship's bay; null uses <see cref="StartupSound"/>.</summary>
    [DataField]
    public SoundSpecifier? AirlockedStartupSound = new SoundPathSpecifier("/Audio/CMU14/Dropship/dropship_flight_airlocked_start.ogg");

    /// <summary>
    /// Flight ambience played on the dropship at the start of travel; null plays none. The CMU dropship flight
    /// audio system starts <see cref="FlightLoopSound"/> once the takeoff cue has finished instead.
    /// </summary>
    [DataField]
    public SoundSpecifier? TravelSound;

    /// <summary>Looping engine sound while the dropship is in flight, after the takeoff cue.</summary>
    [DataField]
    public SoundSpecifier? FlightLoopSound = new SoundPathSpecifier("/Audio/CMU14/Dropship/dropship_flight_recurr.ogg");

    [DataField]
    public SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/CMU14/Dropship/dropship_flight_end.ogg");

    /// <summary>Extra landing sound played aboard the dropship on top of <see cref="ArrivalSound"/>.</summary>
    [DataField]
    public SoundSpecifier? InteriorArrivalSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/Shuttle/engine_landing.ogg",
        AudioParams.Default.AddVolume(5f));

    // Timed to the takeoff and landing sounds, as cmss13-pve#1294 does.
    /// <summary>Engine warmup before leaving the ground; the dropship lifts off as the takeoff cue does.</summary>
    [DataField]
    public float StartupTime = 15f;

    /// <summary>Warmup when dropping out of a warship's bay: the clamps let go four seconds into the cue.</summary>
    [DataField]
    public float AirlockedStartupTime = 4f;

    /// <summary>
    ///     How long the final approach takes; null uses the standard shuttle arrival time. The approach starts
    ///     with dropship_flight_end.ogg (27.5 seconds) and the dropship touches down with nine seconds of it left.
    /// </summary>
    [DataField]
    public float? ArrivalTime = 18.5f;
    // cmu edit end

    [DataField]
    public SoundSpecifier? LaunchAlarmSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/Shuttle/dropship_launch_alarm.ogg")
    {
        Params = AudioParams.Default.WithVolume(2f).WithReferenceDistance(10).WithMaxDistance(30).WithLoop(true),
    };

    /// <summary>
    ///     The faction of the hijacker, if any. Set during FlyTo when hijack=true.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? HijackerFaction;

    /// <summary>
    ///     The faction of the victim (the dropship owner). Set during FlyTo when hijack=true.
    ///     Used by evacuation to scope announcements. Null = default marine.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? VictimFaction;

    /// <summary>
    ///     Whether this was a human-vs-human hijack. Set during FlyTo when hijack=true.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsHumanHijack;

    [DataField, AutoNetworkedField]
    public NetCoordinates? LastLandingCoordinates;

    [DataField, AutoNetworkedField]
    public Vector2i TacticalLandFootprint = new(11, 21);

    /// <summary>Withdrawal evacuation is in flight — cannot be cancelled.</summary>
    [DataField, AutoNetworkedField]
    public bool WithdrawEvacuating;
}
