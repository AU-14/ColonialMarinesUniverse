// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): runtime marker placed on a lazily-created stone level (the map BELOW a
/// dug map). Tracks which chunks have already had their dirt/stone generated so generation is per-chunk
/// and idempotent - each chunk is filled exactly once, on demand, as a digger or nearby player reaches it.
/// </summary>
// NetworkedComponent (presence only): the client uses it to know a map is an underground stone level so the
// structural scanner overlay only draws there. The collection fields stay server-side and are not networked.
[RegisterComponent, NetworkedComponent]
public sealed partial class ZGeneratedStoneComponent : Component
{
    /// <summary>The grid (on this map) that holds the generated stone.</summary>
    [ViewVariables]
    public EntityUid StoneGrid;

    /// <summary>Chunk coordinates (tile-index / chunk size) that have already been generated.</summary>
    [ViewVariables]
    public readonly HashSet<Vector2i> GeneratedChunks = new();

    /// <summary>
    /// Event-driven cave-in: tiles whose roof stability may have changed and need re-evaluating. Populated when
    /// an anchored solid (mined rock, a built/destroyed pillar) changes on this level - the only thing that can
    /// alter roof support. The system evaluates ONLY these tiles (plus already-pending ones) instead of scanning
    /// every dug tile every tick, so an idle underground costs nothing. Drained each evaluation pass.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<Vector2i> DirtyTiles = new();

    /// <summary>
    /// Underground cave-in scheduling: open tiles whose roof is unstable, mapped to the time they will collapse
    /// (8s after they first became unstable). Cleared if the player stabilises the tile (mines less / builds a
    /// pillar) before the timer elapses.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<Vector2i, TimeSpan> PendingCollapse = new();

    /// <summary>
    /// Tiles still to be buried by an in-progress cavern collapse, buried a batch at a time so the collapse is a
    /// big, drawn-out event (sustained shake + rumble) rather than a single instant tile. Populated by flood-fill
    /// of the whole cavern when any unstable tile's warning elapses.
    /// </summary>
    [ViewVariables]
    public readonly List<Vector2i> CollapseQueue = new();

    /// <summary>When the next batch of <see cref="CollapseQueue"/> tiles should be buried.</summary>
    [ViewVariables]
    public TimeSpan CollapseNextStep;

    /// <summary>Throttle for the rumble SFX during an in-progress collapse.</summary>
    [ViewVariables]
    public TimeSpan CollapseNextRumble;

    /// <summary>
    /// The flood-filled cavern region from the most recent cave-in. Populated at collapse start, used at
    /// collapse end to trigger surface effects on the level above. Cleared after surface effects fire.
    /// </summary>
    [ViewVariables]
    public readonly List<Vector2i> LastCollapseRegion = new();
}
