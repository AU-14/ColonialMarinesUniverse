using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>Requests a bounded view around the server's attached actor, never an arbitrary map or position.</summary>
[Serializable, NetSerializable]
public sealed class CMU3DViewRequest(bool enabled, float radius) : EntityEventArgs
{
    public const int MaximumDepth = 8;
    public const float MaximumRadius = 24;
    // Covers the buffered scene plus time for budgeted PVS entries to arrive while walking.
    public const float PrefetchMargin = 8;
    public bool Enabled { get; } = enabled;
    public float Radius { get; } = radius;
}
