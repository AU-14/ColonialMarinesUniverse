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
}
