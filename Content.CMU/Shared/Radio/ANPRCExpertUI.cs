using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Radio;

// ----- expert techniques, sent from the faceplate only ---------------------------------------------
// the guided panel never offers these. the server does not care which view sent them - the view is
// the operator's own preference, so the reward for learning the faceplate is what it can reach

[Serializable, NetSerializable]
public sealed class ANPRCSetBurstMsg(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class ANPRCSetPowerSaveMsg(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

// -1 turns the watch off
[Serializable, NetSerializable]
public sealed class ANPRCSetPriorityWatchMsg(int slot) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class ANPRCSetEmconMsg(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

// -1 on either side turns retrans off
[Serializable, NetSerializable]
public sealed class ANPRCSetRetransMsg(int slotA, int slotB) : BoundUserInterfaceMessage
{
    public readonly int SlotA = slotA;
    public readonly int SlotB = slotB;
}

[Serializable, NetSerializable]
public sealed class ANPRCPeakAntennaMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class ANPRCPeakDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class ANPRCOtarMsg : BoundUserInterfaceMessage;

// the contact's frequency as the panel shows it (unearned digits zeroed). -1 stops dwelling
[Serializable, NetSerializable]
public sealed class ANPRCSetDwellMsg(int kilohertz) : BoundUserInterfaceMessage
{
    public readonly int Kilohertz = kilohertz;
}

[Serializable, NetSerializable]
public sealed class ANPRCJammerBearingMsg : BoundUserInterfaceMessage;

// the guided panel's one expert control: put every faceplate setting back to the factory AUTO
[Serializable, NetSerializable]
public sealed class ANPRCReturnToAutoMsg : BoundUserInterfaceMessage;

// what the faceplate needs beyond the basic settings: the expert techniques and the exact
// readings the guided panel only ever summarises in words
[Serializable, NetSerializable]
public sealed class ANPRCExpertState
{
    public bool Burst;
    public bool PowerSave;
    public int PriorityWatchSlot = -1;
    public bool Emcon;
    public int RetransSlotA = -1;
    public int RetransSlotB = -1;
    public bool AntennaPeaked;

    // the dwelled contact as the panel shows it, -1 when the head is walking
    public int SweepDwellKilohertz = -1;

    // friendly sets in reach still on a superseded key, which an over-the-air rekey would reach
    public int OtarTargets;
    public bool OtarReady;

    // jammer DF: a first bearing is on record, and how far the set has moved from where it was taken
    public bool JammerBearingTaken;
    public float JammerBearingDegrees = float.NaN;
    public float JammerBaselineMoved;
    public float JammerBaselineNeeded;
    public bool Jammed;

    // exact link readout. NaN bearing when nothing is carrying the set
    public string CarrierName = string.Empty;
    public float CarrierBearingDegrees = float.NaN;
    public float CarrierDistance;
    public float DrawPerSecond;
    public float BatteryMinutes = -1f;

    // anything set away from the factory AUTO defaults, so the guided panel can say so and offer
    // the one button that resets it
    public bool OffAuto;
}
