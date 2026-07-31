using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Injuries.Shrapnel;

[ByRefEvent]
public readonly record struct CMUShrapnelChangedEvent(EntityUid Body, EntityUid Part, bool Removed);

[Serializable, NetSerializable]
public sealed partial class CMUShrapnelExtractDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity? PreSelectedPart;
}
