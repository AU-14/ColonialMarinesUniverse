using Content.Shared.DoAfter;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;

[ByRefEvent]
public readonly record struct CMUShrapnelChangedEvent(EntityUid Body, BodyRegion Region, bool Removed);

[ByRefEvent]
public record struct CMUShrapnelMovementDetonationAttemptEvent(
    EntityUid Body,
    BodyRegion Region,
    bool Handled = false);

[Serializable, NetSerializable]
public sealed partial class CMUShrapnelExtractDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public BodyRegion PreSelectedRegion;
}
