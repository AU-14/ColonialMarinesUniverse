using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.TacticalMap;

[DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct TacticalMapBlip
{
    public TacticalMapBlip()
    {
    }

    public TacticalMapBlip(
        Vector2i indices,
        SpriteSpecifier.Rsi? image,
        Color color,
        TacticalMapBlipStatus status,
        SpriteSpecifier.Rsi? background,
        bool hiveLeader,
        int occupantCount = 0,
        int fireteamNumber = 0)
    {
        Indices = indices;
        Image = image;
        Color = color;
        Status = status;
        Background = background;
        HiveLeader = hiveLeader;
        OccupantCount = occupantCount;
        FireteamNumber = fireteamNumber;
    }

    public Vector2i Indices { get; init; }
    public SpriteSpecifier.Rsi? Image { get; init; }
    public Color Color { get; init; }
    public TacticalMapBlipStatus Status { get; init; }
    public SpriteSpecifier.Rsi? Background { get; init; }
    public bool HiveLeader { get; init; }
    public int OccupantCount { get; init; }
    public int FireteamNumber { get; init; }
}
