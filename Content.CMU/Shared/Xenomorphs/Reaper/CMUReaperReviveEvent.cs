using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Xenomorphs.Reaper;

public sealed partial class CMUReaperReviveActionEvent : EntityTargetActionEvent;

[ByRefEvent]
public readonly record struct CMUReaperRevivedEvent;

[ByRefEvent]
public readonly record struct CMUReaperRevivalStartedEvent;

[Serializable, NetSerializable]
public sealed partial class CMUReaperReviveDoAfterEvent : SimpleDoAfterEvent
{
    [DataField] public NetEntity Effect;
}
