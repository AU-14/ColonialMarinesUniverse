using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Sentinel;

public sealed partial class XenoToxicSpitActionEvent : WorldTargetActionEvent;

public sealed partial class XenoToxicSlashActionEvent : InstantActionEvent;

public sealed partial class XenoDrainStingActionEvent : EntityTargetActionEvent;

public sealed partial class XenoIntoxicatedResistAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public sealed partial class XenoIntoxicatedResistDoAfterEvent : SimpleDoAfterEvent;
