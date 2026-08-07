using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Round.Objectives;

[Serializable, NetSerializable]
public sealed partial class InteractObjectiveDoAfterEvent : SimpleDoAfterEvent
{
    public string Faction = string.Empty;
    public NetEntity InteractTarget;
}
