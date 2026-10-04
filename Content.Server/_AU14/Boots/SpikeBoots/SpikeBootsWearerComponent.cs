using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._AU14.Boots.SpikeBoots;

[RegisterComponent]
public sealed partial class SpikeBootsWearerComponent : Component
{
    /// <summary>
    /// Tile the wearer occupied last update. Starts impossible so the first movement is always detected.
    /// </summary>
    public Vector2i LastTile = new(int.MinValue, int.MinValue);

    /// <summary>
    /// Per-target damage cooldown expiry times. Prevents re-damaging the same entity within 2 seconds
    /// even if the wearer leaves and re-enters the tile rapidly.
    /// </summary>
    public Dictionary<EntityUid, TimeSpan> TargetCooldowns = new();
}
