using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Objectives.Type;

public enum HotspotVisuals : byte
{
    Controller,
}

/// <summary>
/// Presence-based zone objective: the strongest alive force inside the rectangle scores
/// per tick, and the zone relocates between objective markers to keep both sides moving.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HotspotObjectiveComponent : Robust.Shared.GameObjects.Component
{
    /// <summary>Spawns automatically from a random objective marker instead of map placement.</summary>
    [DataField] public bool Catalog { get; private set; }

    /// <summary>Half-width in tiles, for both the capture AABB and the tacmap rectangle.</summary>
    [DataField] public int HalfWidth { get; private set; } = 7;

    /// <summary>Half-height in tiles; 0 uses HalfWidth.</summary>
    [DataField] public int HalfHeight { get; private set; } = 0;

    [DataField] public float TickSeconds { get; private set; } = 10f;

    [DataField] public int PointsPerTick { get; private set; } = 1;

    [DataField] public bool NormalizeByTeamSize { get; private set; } = true;

    /// <summary>Feeds scored points into the faction win-point pool.</summary>
    [DataField] public bool FeedWinPoints { get; private set; }

    /// <summary>Zone color on the tactical map.</summary>
    [DataField] public string ZoneColorHex { get; private set; } = "#FFB300";

    /// <summary>Tacmap rectangle color per controlling faction; falls back to <see cref="ZoneColorHex"/> when unmatched.</summary>
    [DataField] public Dictionary<string, string> FactionZoneColors { get; private set; } = new();

    /// <summary>Neutral floor tile painted inside the zone.</summary>
    [DataField] public string ZoneTile { get; private set; } = "FloorBlueCircuit";

    /// <summary>Floor tile per controlling faction; falls back to <see cref="ZoneTile"/> when unmatched.</summary>
    [DataField] public Dictionary<string, string> FactionZoneTiles { get; private set; } = new();

    [DataField] public float NormalizationCap { get; private set; } = 3f;

    /// <summary>Move the zone to a fresh marker after this many elapsed ticks; 0 is static.</summary>
    [DataField] public int RelocateAfterTicks { get; private set; }

    /// <summary>Round time after which engagement tiers flatten to 1.0x; zero flattens the whole round.</summary>
    [DataField] public TimeSpan TierDampenAfter { get; private set; } = TimeSpan.FromMinutes(75);

    public string CurrentController = string.Empty;

    // Counts every tick, scored or not, so contested zones still relocate.
    public int ElapsedTicks;

    // Announcements and the faction recolor wait for 2 consecutive ticks so one-tick flips stay silent.
    public int ControllerStreak;

    // Highest milestone announced per faction (25/50/75%); 100% is the win itself.
    public Dictionary<string, int> AnnouncedMilestones = new();

    // Hex of the tacmap rectangle currently drawn, so a recolor removes the old rect.
    public string CurrentMapColorHex = string.Empty;

    public float TickAccumulator;

    public Dictionary<string, int> TicksPerFaction { get; set; } = new();
}
