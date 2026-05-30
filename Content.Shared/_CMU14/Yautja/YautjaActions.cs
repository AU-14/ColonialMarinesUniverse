using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaToggleVisorActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleMaskZoomActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleCloakActionEvent : InstantActionEvent;

public sealed partial class YautjaOpenMarkPanelActionEvent : InstantActionEvent;

public sealed partial class YautjaOpenBracerMenuActionEvent : InstantActionEvent;

public sealed partial class YautjaRecallActionEvent : InstantActionEvent;

public sealed partial class YautjaSelfDestructActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerLockActionEvent : InstantActionEvent;

public sealed partial class YautjaTranslatorActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerIdChipActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateStabilisingCrystalActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHumanStabilisingCrystalActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHealingCapsuleActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHuntingTrapActionEvent : InstantActionEvent;

public sealed partial class YautjaLinkThrallBracerActionEvent : InstantActionEvent;

public sealed partial class YautjaTransmitThrallMessageActionEvent : InstantActionEvent;

public sealed partial class YautjaStunThrallActionEvent : InstantActionEvent;

public sealed partial class YautjaSelfDestructThrallActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleThrallBracerLockActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleCasterActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleWristBladesActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleScimitarActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleShieldActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleChainGauntletActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceClickActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceRoarActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceLaughActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceGrowlActionEvent : InstantActionEvent;

public sealed partial class YautjaVoicePainActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceDeathCryActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceDeathLaughActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationRushActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationRoarActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationToggleFrenzyModeActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationSmashActionEvent : EntityTargetActionEvent;

public sealed partial class YautjaAbominationFrenzyActionEvent : EntityTargetActionEvent;

[ByRefEvent]
public readonly record struct YautjaBracerUnequippedEvent(EntityUid User, SlotFlags SlotFlags);

[Serializable, NetSerializable]
public enum YautjaMarkUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaThrallMessageUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaTranslatorUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaBracerUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaBracerPanelCommand : byte
{
    OpenMarks,
    LinkThrallBracer,
    OpenThrallTransmission,
    StunThrall,
    ToggleThrallSelfDestruct,
    ToggleThrallBracerLock,
    OpenTranslator,
    ToggleBracerLock,
    ToggleBracerIdChip,
    CreateStabilisingCrystal,
    CreateHumanStabilisingCrystal,
    CreateHealingCapsule,
    CreateHuntingTrap,
    ToggleSelfDestruct,
    RefreshTracker,
}

[Serializable, NetSerializable]
public sealed class YautjaBracerPanelState(
    int charge,
    int maxCharge,
    bool locked,
    bool idChipDeployed,
    bool selfDestructArmed,
    string? thrallName,
    bool thrallLinked,
    bool thrallSelfDestructArmed,
    bool thrallBracerLocked,
    List<YautjaGearTrackerEntry> trackedGear) : BoundUserInterfaceState
{
    public readonly int Charge = charge;
    public readonly int MaxCharge = maxCharge;
    public readonly bool Locked = locked;
    public readonly bool IdChipDeployed = idChipDeployed;
    public readonly bool SelfDestructArmed = selfDestructArmed;
    public readonly string? ThrallName = thrallName;
    public readonly bool ThrallLinked = thrallLinked;
    public readonly bool ThrallSelfDestructArmed = thrallSelfDestructArmed;
    public readonly bool ThrallBracerLocked = thrallBracerLocked;
    public readonly List<YautjaGearTrackerEntry> TrackedGear = trackedGear;
}

[Serializable, NetSerializable]
public sealed class YautjaGearTrackerEntry(string name, byte direction, int distance, int bearing, int count = 1)
{
    public readonly string Name = name;
    public readonly byte Direction = direction;
    public readonly int Distance = distance;
    public readonly int Bearing = bearing;
    public readonly int Count = count;
}

[Serializable, NetSerializable]
public sealed class YautjaBracerPanelRefreshMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class YautjaBracerPanelCommandMsg(YautjaBracerPanelCommand command) : BoundUserInterfaceMessage
{
    public readonly YautjaBracerPanelCommand Command = command;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelState(List<YautjaMarkPanelEntry> entries) : BoundUserInterfaceState
{
    public readonly List<YautjaMarkPanelEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelEntry(NetEntity entity, string name, bool isXeno, List<YautjaMarkKind> marks)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly bool IsXeno = isXeno;
    public readonly List<YautjaMarkKind> Marks = marks;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelRefreshMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelMarkMsg(NetEntity target, YautjaMarkKind kind, string? reason) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly YautjaMarkKind Kind = kind;
    public readonly string? Reason = reason;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelUnmarkMsg(NetEntity target, YautjaMarkKind kind) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly YautjaMarkKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed class YautjaThrallSendMessageMsg(string message) : BoundUserInterfaceMessage
{
    public readonly string Message = message;
}

[Serializable, NetSerializable]
public sealed class YautjaTranslatorBuiState(int charge, int maxCharge, int cost, int maxLength) : BoundUserInterfaceState
{
    public readonly int Charge = charge;
    public readonly int MaxCharge = maxCharge;
    public readonly int Cost = cost;
    public readonly int MaxLength = maxLength;
}

[Serializable, NetSerializable]
public sealed class YautjaTranslatorSendMessageMsg(string message) : BoundUserInterfaceMessage
{
    public readonly string Message = message;
}

[ByRefEvent]
public record struct YautjaMarkAttemptEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, string? Reason, bool Cancelled = false);

[ByRefEvent]
public record struct YautjaMarkAppliedEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, string? Reason);

[ByRefEvent]
public record struct YautjaMarkRemoveAttemptEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, bool Cancelled = false);

[ByRefEvent]
public record struct YautjaMarkRemovedEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind);
