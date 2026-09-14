using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Objectives.Type;

/// <summary>
/// Presence-based zone objective: the strongest alive force inside the radius scores points
/// every tick. Optional team-size normalization keeps outnumbered sides competitive, and the
/// zone can relocate between mapper-placed objective markers to keep both teams moving.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class HotspotObjectiveComponent : Robust.Shared.GameObjects.Component
{
    /// <summary>Spawns automatically from a random objective marker instead of map placement.</summary>
    [DataField] public bool Catalog { get; private set; }

    /// <summary>
    /// Half-width of the rectangular zone in tiles. Used for both the tacmap visual
    /// and the capture presence check (AABB). Default 7 = 14×14 tile zone.
    /// </summary>
    [DataField] public int HalfWidth { get; private set; } = 7;

    /// <summary>
    /// Half-height of the rectangular zone in tiles. If 0, uses HalfWidth (square).
    /// </summary>
    [DataField] public int HalfHeight { get; private set; } = 0;

    [DataField] public float TickSeconds { get; private set; } = 10f;

    [DataField] public int PointsPerTick { get; private set; } = 1;

    [DataField] public bool NormalizeByTeamSize { get; private set; } = true;

    /// <summary>
    /// Also feeds scored points into the faction win-point pool. Off by default: that pool's
    /// threshold is muted and other objective types already feed it, so hotspot points are
    /// tallied separately on the objective master instead.
    /// </summary>
    [DataField] public bool FeedWinPoints { get; private set; }

    /// <summary>
    /// Zone color on the tactical map (amber).
    /// </summary>
    [DataField] public string ZoneColorHex { get; private set; } = "#FFB300";

    [DataField] public float NormalizationCap { get; private set; } = 3f;

    /// <summary>Move the zone to a fresh marker after this many scoring ticks; 0 is static.</summary>
    [DataField] public int RelocateAfterTicks { get; private set; }

    [AutoNetworkedField] public string CurrentController = string.Empty;

    [AutoNetworkedField] public int TicksScored;

    public float TickAccumulator;

    public Dictionary<string, int> TicksPerFaction { get; set; } = new();
}
