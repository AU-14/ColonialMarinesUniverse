using Content.Shared.DoAfter;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

[Serializable, NetSerializable]
public enum ANPRCRadioUI
{
    Key,
}

[Serializable, NetSerializable]
public enum RadioMode : byte
{
    FrequencyHopping,
    SingleChannel,
    CipherText,
    PlainText,
}

public static class RadioModeExtensions
{
    public static float ChargeMultiplier(this RadioMode mode) => mode switch
    {
        RadioMode.FrequencyHopping => 1.5f,
        RadioMode.SingleChannel => 0.75f,
        RadioMode.PlainText => 0.5f,
        _ => 1f,
    };
}

[Serializable, NetSerializable]
public enum RadioTxPower : byte
{
    Low,
    Medium,
    High,
}

public static class RadioTxPowerExtensions
{
    public static float RangeMultiplier(this RadioTxPower power) => power switch
    {
        RadioTxPower.Low => 0.6f,
        RadioTxPower.High => 1.5f,
        _ => 1f,
    };

    public static float ChargeMultiplier(this RadioTxPower power) => power switch
    {
        RadioTxPower.Low => 0.5f,
        RadioTxPower.High => 2f,
        _ => 1f,
    };

    public static float DFMultiplier(this RadioTxPower power) => power switch
    {
        RadioTxPower.Low => 0.5f,
        RadioTxPower.High => 2f,
        _ => 1f,
    };

    public static string Short(this RadioTxPower power) => power switch
    {
        RadioTxPower.Low => "LO",
        RadioTxPower.High => "HI",
        _ => "MED",
    };
}

[Serializable, NetSerializable]
public sealed class ANPRCNetLogEntry(float timestamp, string senderName, string channelDisplay, string message)
{
    public readonly float Timestamp = timestamp;
    public readonly string SenderName = senderName;
    public readonly string ChannelDisplay = channelDisplay;
    public readonly string Message = message;
}

[Serializable, NetSerializable]
public sealed class ANPRCSelectSlotMsg(int slot) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class ANPRCTogglePowerMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ANPRCToggleMonitorMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ANPRCSetModeMsg(RadioMode mode) : BoundUserInterfaceMessage
{
    public readonly RadioMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class ANPRCSetScanMsg(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class ANPRCSetTxPowerMsg(RadioTxPower power) : BoundUserInterfaceMessage
{
    public readonly RadioTxPower Power = power;
}

[Serializable, NetSerializable]
public sealed partial class ANPRCPlantDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ANPRCPackUpDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class ANPRCSetSquelchMsg(int level) : BoundUserInterfaceMessage
{
    public readonly int Level = level;
}

[Serializable, NetSerializable]
public sealed class ANPRCSetCallsignMsg(string callsign) : BoundUserInterfaceMessage
{
    public readonly string Callsign = callsign;
}

[Serializable, NetSerializable]
public sealed class ANPRCAddSlotMsg(string label) : BoundUserInterfaceMessage
{
    public readonly string Label = label;
}

[Serializable, NetSerializable]
public sealed class ANPRCDeleteSlotMsg(int slot) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class ANPRCSetSlotChannelMsg(int slot, ProtoId<RadioChannelPrototype> channel) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
    public readonly ProtoId<RadioChannelPrototype> Channel = channel;
}

[Serializable, NetSerializable]
public sealed class ANPRCClearSlotMsg(int slot) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class ANPRCCryptoZeroizeMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ANPRCCryptoDestroyMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ANPRCCryptoRecryptoMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ANPRCRadioCheckMsg : BoundUserInterfaceMessage;

// pops the read-only comms net directory carried by the pack
[Serializable, NetSerializable]
public sealed class ANPRCOpenDirectoryMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ANPRCManualFrequencyMsg(int slot, string frequencyText) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
    public readonly string FrequencyText = frequencyText;
}

[Serializable, NetSerializable]
public sealed class ANPRCRadioState(
    Dictionary<int, ProtoId<RadioChannelPrototype>> presets,
    Dictionary<int, int> frequencyOverrides,
    Dictionary<int, string> slotLabels,
    int activeSlot,
    bool enabled,
    bool isEquipped,
    bool monitorEnabled,
    RadioMode mode,
    bool scanEnabled,
    int squelchLevel,
    RadioTxPower txPower,
    bool planted,
    string callsign,
    string wearerCallsign,
    List<string> callsignPresets,
    string cryptoDesignation,
    string cryptoFaction,
    bool cryptoStale,
    string operatorFaction,
    List<ANPRCNetLogEntry> netLog,
    float batteryFraction,
    bool hasBattery,
    string antennaLabel,
    Dictionary<string, int> channelFrequencies)
    : BoundUserInterfaceState
{
    public readonly Dictionary<int, ProtoId<RadioChannelPrototype>> Presets = presets;
    public readonly Dictionary<int, int> FrequencyOverrides = frequencyOverrides;
    public readonly Dictionary<int, string> SlotLabels = slotLabels;
    public readonly int ActiveSlot = activeSlot;
    public readonly bool Enabled = enabled;
    public readonly bool IsEquipped = isEquipped;
    public readonly bool MonitorEnabled = monitorEnabled;
    public readonly RadioMode Mode = mode;
    public readonly bool ScanEnabled = scanEnabled;
    public readonly int SquelchLevel = squelchLevel;
    public readonly RadioTxPower TxPower = txPower;
    public readonly bool Planted = planted;
    public readonly string Callsign = callsign;

    public readonly string WearerCallsign = wearerCallsign;

    public readonly List<string> CallsignPresets = callsignPresets;
    public readonly string CryptoDesignation = cryptoDesignation;
    public readonly string CryptoFaction = cryptoFaction;
    public readonly bool CryptoStale = cryptoStale;
    public readonly string OperatorFaction = operatorFaction;
    public readonly List<ANPRCNetLogEntry> NetLog = netLog;
    public readonly float BatteryFraction = batteryFraction;
    public readonly bool HasBattery = hasBattery;
    public readonly string AntennaLabel = antennaLabel;

    // the round's signal plan: channel id -> live frequency. prototype frequencies
    // are only the book values, the plan is rolled per round
    public readonly Dictionary<string, int> ChannelFrequencies = channelFrequencies;
}
