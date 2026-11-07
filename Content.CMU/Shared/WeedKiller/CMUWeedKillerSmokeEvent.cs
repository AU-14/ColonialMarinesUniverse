using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.WeedKiller;

/// <summary>Short-lived cosmetic smoke. Weed destruction and area suppression remain server-authoritative.</summary>
[Serializable, NetSerializable]
public sealed class CMUWeedKillerSmokeEvent(NetEntity grid, Vector2i[] tiles, TimeSpan startedAt) : EntityEventArgs
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(1.5);
    public NetEntity Grid { get; } = grid;
    public Vector2i[] Tiles { get; } = tiles;
    public TimeSpan StartedAt { get; } = startedAt;
}
