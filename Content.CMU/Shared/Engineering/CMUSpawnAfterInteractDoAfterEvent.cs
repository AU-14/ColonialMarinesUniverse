using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Engineering;

[Serializable, NetSerializable]
public sealed partial class CMUSpawnAfterInteractDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates Coordinates;

    public CMUSpawnAfterInteractDoAfterEvent(NetCoordinates coordinates) => Coordinates = coordinates;
}
