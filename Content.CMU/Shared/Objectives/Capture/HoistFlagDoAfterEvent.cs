using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.AU14.Objectives.Capture;

[Serializable, NetSerializable]

public sealed partial class HoistFlagDoAfterEvent : SimpleDoAfterEvent
{
    public string Faction = string.Empty;
}
