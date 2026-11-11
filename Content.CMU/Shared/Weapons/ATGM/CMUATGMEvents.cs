using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     Sent by a guiding spotter's client with their current cursor position. The server resolves the
///     guider from the sending session; nothing in this message identifies an entity to act on.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUATGMGuidanceEvent(NetCoordinates coordinates) : EntityEventArgs
{
    public NetCoordinates Coordinates = coordinates;
}

/// <summary>
///     Loading a missile into a deployed A.T.G.M. Used is the missile.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUATGMLoadDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Wiring a finder to a deployed A.T.G.M. Used is the finder.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CMUATGMLinkDoAfterEvent : SimpleDoAfterEvent;
