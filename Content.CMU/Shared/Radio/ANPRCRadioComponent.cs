using System.Numerics;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ANPRCRadioComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, string> SlotLabels = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, ProtoId<RadioChannelPrototype>> Presets = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, int> FrequencyOverrides = new();

    public const int MaxSlots = 4;

    [DataField, AutoNetworkedField]
    public int ActiveSlot = -1;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public string RequiredSlot = "back";

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
    public float TransmitChargeCost = 10f;

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

    #region Band sweep

    // search receiver. walking the band ties up the set: no transmit, no traffic on
    // your own nets. finding somebody else's net is a job, not a passive perk
    [DataField, AutoNetworkedField]
    public bool SweepEnabled;

    // where the sweep head currently sits, in raw frequency units
    [DataField, AutoNetworkedField]
    public int SweepPosition = SweepBandMin;

    public const int SweepBandMin = 1000;
    public const int SweepBandMax = 2999;

    // the colonist softwave band, where handhelds and tunable headsets live. FREQ
    // accepts it so a set can bridge a headset direct net, but the search receiver
    // does not cover it
    public const int SoftwaveBandMin = 30000;
    public const int SoftwaveBandMax = 87999;

    // frequency -> how much of a fix the operator has built on it. what that buys is
    // set by SweepTierThresholds: the number falls in one digit at a time
    public Dictionary<int, float> SweepContacts = new();

    // frequencies the operator has fixed exactly. these become tunable by name
    [DataField]
    public HashSet<int> DiscoveredFrequencies = new();

    // band units the head advances per second. the full band takes
    // (SweepBandMax - SweepBandMin) / this seconds to cover once
    [DataField("sweepStepPerSecond")]
    public float SweepStepPerSecond = 100f;

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
