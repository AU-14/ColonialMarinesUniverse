// Content.Shared/_RMC14/Xenonids/Charge/CursorCharge/XenoCursorSteeringMessage.cs

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

[Serializable, NetSerializable]
public sealed class XenoCursorSteeringMessage : EntityEventArgs
{
    public Vector2 CursorWorldPosition;
}
