using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaToggleVisorActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleMaskZoomActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleCloakActionEvent : InstantActionEvent;

public sealed partial class YautjaOpenMarkPanelActionEvent : InstantActionEvent;

public sealed partial class YautjaRecallActionEvent : InstantActionEvent;

public sealed partial class YautjaSelfDestructActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerLockActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerIdChipActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateStabilisingCrystalActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHumanStabilisingCrystalActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHealingCapsuleActionEvent : InstantActionEvent;

public sealed partial class YautjaLinkThrallBracerActionEvent : InstantActionEvent;

public sealed partial class YautjaTransmitThrallMessageActionEvent : InstantActionEvent;

public sealed partial class YautjaStunThrallActionEvent : InstantActionEvent;

public sealed partial class YautjaSelfDestructThrallActionEvent : InstantActionEvent;

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

[ByRefEvent]
public record struct YautjaMarkAttemptEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, string? Reason, bool Cancelled = false);

[ByRefEvent]
public record struct YautjaMarkAppliedEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, string? Reason);

[ByRefEvent]
public record struct YautjaMarkRemoveAttemptEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, bool Cancelled = false);

[ByRefEvent]
public record struct YautjaMarkRemovedEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind);
