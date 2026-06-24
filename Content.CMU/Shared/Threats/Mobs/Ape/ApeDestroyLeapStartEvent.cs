using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Ape;

[Serializable, NetSerializable]
public sealed class ApeDestroyLeapStartEvent(NetEntity king, Vector2 leapOffset) : EntityEventArgs
{
    public readonly NetEntity King = king;

    public readonly Vector2 LeapOffset = leapOffset;
}

