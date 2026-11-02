using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

[Serializable, NetSerializable]
public sealed class CMUFobMapMarkersMessage(Dictionary<int, TacticalMapBlip> markers) : BoundUserInterfaceMessage
{
    public readonly Dictionary<int, TacticalMapBlip> Markers = markers;
}
