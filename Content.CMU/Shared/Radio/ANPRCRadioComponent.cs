using System.Numerics;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ANPRCRadioComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, string> SlotLabels = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, ProtoId<RadioChannelPrototype>> Presets = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, RadioFrequency> FrequencyOverrides = new();

    public const int MaxSlots = 4;

    [DataField, AutoNetworkedField]
    public int ActiveSlot = -1;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public string RequiredSlot = "back";

    [DataField]
    public bool RelayOnly;

    [DataField, AutoNetworkedField]
    public bool IsEquipped = false;

    [DataField, AutoNetworkedField]
    public bool MonitorEnabled = false;

    [DataField, AutoNetworkedField]
    public RadioMode Mode = RadioMode.FrequencyHopping;

    [DataField, AutoNetworkedField]
    public bool ScanEnabled = false;

    [DataField, AutoNetworkedField]
    public RadioTxPower TxPower = RadioTxPower.Medium;

    [DataField, AutoNetworkedField]
    public bool Planted = false;

    [DataField, AutoNetworkedField]
    public int SquelchLevel = 3;

    public const int MaxSquelchLevel = 4;

    [DataField, AutoNetworkedField]
    public string Callsign = string.Empty;

    [DataField, AutoNetworkedField]
    public List<string> CallsignPresets = new();

    public const int MaxCallsignLength = 16;

    public EntityUid? HandsetUser;

    [DataField]
    public EntProtoId HandsetId = "AU14ANPRCHandset";

    public const string HandsetContainerId = "anprc_handset";

    [AutoNetworkedField]
    public EntityUid? Handset;

    public bool NameMaskActive;

    public const int MaxLabelLength = 8;

    [DataField, AutoNetworkedField]
    public string OperatorFaction = string.Empty;

    [DataField("transmitChargeCost")]
    public float TransmitChargeCost = 18f;

    // draw per second while the set is switched on and worn or staked. the set owns its idle
    // drain rather than PowerCellDraw, so POWER SAVE and EMCON can turn it down
    [DataField]
    public float IdleChargePerSecond = 2f;

    // extra draw per second while the set is anchoring nets for the headsets around it, on top
    // of the idle draw. scaled by TX power, so a set shouting on HIGH for the whole platoon pays for it
    [DataField]
    public float RelayChargePerSecond = 1f;

    #region Expert techniques

    // everything in this region is worked from the faceplate only. the guided panel shows what
    // is set and offers one button to put the lot back to AUTO, never the controls themselves

    // BURST: each sentence goes out compressed. harder to direction-find and cheaper to send
    [DataField, AutoNetworkedField]
    public bool Burst;

    [DataField]
    public float BurstDFMultiplier = 0.5f;

    [DataField]
    public float BurstChargeMultiplier = 0.7f;

    // POWER SAVE: the receiver duty-cycles between messages. costs SCAN and PRIORITY WATCH,
    // which need the receiver awake
    [DataField, AutoNetworkedField]
    public bool PowerSave;

    [DataField]
    public float PowerSaveIdleMultiplier = 0.6f;

    // PRIORITY WATCH: a second memory the operator hears while working the active one. -1 off
    [DataField, AutoNetworkedField]
    public int PriorityWatchSlot = -1;

    // EMCON: listen-silent. no transmitting, no relaying, nothing to direction-find, and the
    // set sips power. it still hears and logs everything
    [DataField, AutoNetworkedField]
    public bool Emcon;

    [DataField]
    public float EmconIdleMultiplier = 0.25f;

    // RETRANS: a staked set repeats traffic between two of its memories. -1 off
    [DataField, AutoNetworkedField]
    public int RetransSlotA = -1;

    [DataField, AutoNetworkedField]
    public int RetransSlotB = -1;

    // ANTENNA PEAK: a staked set whose antenna has been aimed and tuned covers further, until it
    // is packed up
    [DataField, AutoNetworkedField]
    public bool AntennaPeaked;

    [DataField]
    public float PeakRangeMultiplier = 1.25f;

    [DataField]
    public TimeSpan PeakDelay = TimeSpan.FromSeconds(15);

    // OTAR: push a recrypto's new key to friendly sets over the air
    [DataField]
    public float OtarChargeCost = 100f;

    [DataField]
    public TimeSpan OtarCooldown = TimeSpan.FromSeconds(30);

    public TimeSpan OtarLast;

    // JAMMER DF: two bearings on the same jammer from far enough apart fix it on the map
    public EntityUid? JammerBearingTarget;

    public System.Numerics.Vector2 JammerBearingPosition;

    public TimeSpan JammerBearingTime;

    [DataField]
    public float JammerFixBaseline = 12f;

    [DataField]
    public TimeSpan JammerBearingExpiry = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan JammerFixDuration = TimeSpan.FromMinutes(2);

    #endregion

    [DataField("dfReportFactions")]
    public List<string> DFReportFactions = new();

    [DataField("dfPingDuration")]
    public TimeSpan DFPingDuration = TimeSpan.FromSeconds(20);

    [DataField("dfChancePlainText")]
    public float DFChancePlainText = 0.08f;

    [DataField("dfChanceUnsecured")]
    public float DFChanceUnsecured = 0.05f;

    [DataField("dfChanceSecuredFH")]
    public float DFChanceSecuredFH = 0.03f;

    [DataField("dfChanceJamBonus")]
    public float DFChanceJamBonus = 0.12f;

    [DataField("dfAccumBonus")]
    public float DFAccumBonus = 0.04f;

    [DataField("dfAccumDecay")]
    public TimeSpan DFAccumDecay = TimeSpan.FromSeconds(60);

    [DataField("dfAccumResetDistance")]
    public float DFAccumResetDistance = 10f;

    public float DFAccumulation;

    public TimeSpan DFLastTransmitTime;

    public Vector2 DFLastTransmitPos;

    public Queue<ANPRCNetLogEntry> NetLog = new();

    public const int MaxNetLogEntries = 50;

    public HashSet<string> GrantedChannels = new();

    // slots the set ships with, seeded at map init. a base station has to be usable
    // the moment it is set up - the cell leader manning it is not a trained operator
    // and cannot open the panel to tune it
    [DataField]
    public List<ANPRCDefaultSlot> DefaultSlots = new();

    // the nets the panel's quick setup loads besides the wearer's own squad net. empty
    // falls back to DefaultSlots, so a set that ships tuned sets up the same way
    [DataField]
    public List<ANPRCDefaultSlot> StandardNets = new();

    // the link and battery an open panel was last told about, so the refresh only pushes
    // state when one of them moved. NaN is never-sent
    public float PanelLinkQuality = float.NaN;

    public float PanelBatteryFraction = float.NaN;

    // when the set last keyed up and last took traffic, so the panel lights its TX and RX
    // lamps off what the radio actually did rather than off a UI guess
    public TimeSpan LastTransmit;

    public TimeSpan LastReceive;

    #region Band sweep

    // search receiver. walking the band ties up the set: no transmit, no traffic on
    // your own nets. finding somebody else's net is a job, not a passive perk
    [DataField, AutoNetworkedField]
    public bool SweepEnabled;

    // where the sweep head currently sits
    [DataField, AutoNetworkedField]
    public RadioFrequency SweepPosition = SweepBandMin;

    public static readonly RadioFrequency SweepBandMin = RadioFrequency.FromKilohertz(100_000);
    public static readonly RadioFrequency SweepBandMax = RadioFrequency.FromKilohertz(299_900);

    // the colonist softwave band, where handhelds and tunable headsets live. FREQ
    // accepts it so a set can bridge a headset direct net, but the search receiver
    // does not cover it
    public static readonly RadioFrequency SoftwaveBandMin = RadioFrequency.FromKilohertz(30_000);
    public static readonly RadioFrequency SoftwaveBandMax = RadioFrequency.FromKilohertz(87_999);

    // frequency -> how much of a fix the operator has built on it. what that buys is
    // set by SweepTierThresholds: the number falls in one digit at a time
    public Dictionary<RadioFrequency, float> SweepContacts = new();

    // frequencies the operator has fixed exactly. these become tunable by name
    [DataField]
    public HashSet<RadioFrequency> DiscoveredFrequencies = new();

    // kHz the head advances per second. the full 100-299.9 MHz band takes about 20 seconds to cover
    [DataField]
    public int SweepKilohertzPerSecond = 10_000;

    // how recently a frequency must have carried traffic for the passing head to
    // catch it. short window plus a slow head means most passes come up empty
    [DataField("sweepActivityWindow")]
    public TimeSpan SweepActivityWindow = TimeSpan.FromSeconds(20);

    // how far away a transmitter can be and still be intercepted
    [DataField("sweepInterceptRange")]
    public float SweepInterceptRange = 60f;

    [DataField("sweepConfidencePerHit")]
    public float SweepConfidencePerHit = 0.25f;

    // contacts rot, but slower than a caught pass builds them, so progress survives
    // between flybys and a net that goes quiet loses its fix over a few minutes
    [DataField("sweepConfidenceDecayPerSecond")]
    public float SweepConfidenceDecayPerSecond = 0.005f;

    // a busy net is easier to fix than a disciplined one. traffic caught in the
    // window multiplies the hit up to this ceiling, so chatter is what gets you found
    [DataField("sweepTrafficBonusPerEmission")]
    public float SweepTrafficBonusPerEmission = 0.5f;

    [DataField("sweepTrafficMultiplierMax")]
    public float SweepTrafficMultiplierMax = 2f;

    // confidence gates for each step of the fix. the head gives the number up a digit
    // at a time - band half, hundreds, tens, then the exact frequency and the net's
    // name. below the first entry a contact is not shown at all. one entry per digit,
    // so a shorter or longer ladder just changes how many steps there are
    [DataField("sweepTierThresholds")]
    public List<float> SweepTierThresholds = new() { 0.25f, 0.75f, 1.25f, 2f };

    public float SweepResolveThreshold =>
        SweepTierThresholds.Count > 0 ? SweepTierThresholds[^1] : 1f;

    [DataField("sweepChargeCostPerSecond")]
    public float SweepChargeCostPerSecond = 3f;

    // DWELL: the head parked on one contact instead of walking the band. -1 kHz is off. each fresh
    // burst of traffic on it counts this many times over, but nothing else on the band is heard
    [DataField, AutoNetworkedField]
    public int SweepDwellKilohertz = -1;

    public TimeSpan SweepDwellLastEmission;

    [DataField]
    public float DwellConfidenceMultiplier = 2f;

    public TimeSpan SweepLastUpdate;

    #endregion
}

[DataDefinition]
public sealed partial class ANPRCDefaultSlot
{
    [DataField(required: true)]
    public string Label = string.Empty;

    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> Channel;
}
