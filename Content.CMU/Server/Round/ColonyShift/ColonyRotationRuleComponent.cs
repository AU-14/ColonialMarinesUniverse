using Content.Shared.CMU14;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Round.ColonyShift;

// greenshift ending: the crew rotation transport comes down at the end of the shift, waits on a pad, leaves,
// and the round ends with who rode it out plus whatever cargo got loaded aboard
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(ColonyRotationRuleSystem))]
public sealed partial class ColonyRotationRuleComponent : Component
{
    [DataField]
    public ResPath ShuttlePath = new("/Maps/CMU14/Dropships/civshuttleextraseating.yml");

    [DataField]
    public EntProtoId ReturnDestination = "CMDropshipDestinationThirdPartyReturn";

    // shift length before the company sends the transport on its own
    [DataField]
    public TimeSpan AutoDispatchDelay = TimeSpan.FromMinutes(90);

    // the admin can't call it in before this, so a round can't be ended in the first few minutes
    [DataField]
    public TimeSpan EarliestCall = TimeSpan.FromMinutes(20);

    // dispatch -> landing. recall only works inside this window, once the shuttle's loaded it's committed
    [DataField]
    public TimeSpan InboundTime = TimeSpan.FromMinutes(10);

    // recalling pushes the next automatic dispatch back by this much
    [DataField]
    public TimeSpan RecallExtension = TimeSpan.FromMinutes(30);

    [DataField]
    public TimeSpan DockTime = TimeSpan.FromMinutes(8);

    [DataField]
    public TimeSpan CallCooldown = TimeSpan.FromMinutes(1);

    [DataField]
    public float FlightStartup = 10f;

    [DataField]
    public float FlightTime = 30f;

    // round ends once it's in hyperspace on the way out, this is only the fallback if that never shows
    [DataField]
    public TimeSpan DepartureTimeout = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    // docked countdown announcements, time left on the pad
    [DataField]
    public List<TimeSpan> DockWarnings = new() { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30) };

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    [DataField]
    public ColonyRotationTransportStatus Status = ColonyRotationTransportStatus.Scheduled;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan AutoDispatchAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EarliestCallAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ArriveAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DepartAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAttempt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCallAllowed;

    [DataField]
    public int NextDockWarning;

    [DataField]
    public EntityUid? Shuttle;

    [DataField]
    public EntityUid? ReturnPad;

    [DataField]
    public EntityUid? Pad;

    // filled in when the round ends so the round-end text and tests read the same numbers
    [ViewVariables]
    public List<string> RotatedOut = new();

    [ViewVariables]
    public int StayedBehind;

    [ViewVariables]
    public Dictionary<string, int> ShipmentCounts = new();

    [ViewVariables]
    public float ShipmentValue;
}
