using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Alchemist;

public sealed partial class XenoSelectChemicalActionEvent : InstantActionEvent;

public sealed partial class XenoProduceChemicalActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class XenoProduceChemicalDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class XenoRemoveChemicalActionEvent : InstantActionEvent;

public sealed partial class XenoTailInjectionActionEvent : EntityTargetActionEvent;
