// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): marks a piece of mapper-authored TERRAIN as holding up a cave roof, exactly
/// like natural rock and walls do for the cave-in system (<c>ZCaveInSystem</c>).
///
/// Needed because some terrain is deliberately NOT anchored. Shepherd's underground river is built from
/// thousands of <c>CMUEntityShepDesertWaterDeep</c> entities saved with <c>anchored: False</c> (water must not
/// occupy the snap grid or block movement), so the cave-in system's anchored-entity scan saw nothing on those
/// tiles and read the entire riverbed as one enormous open cavern. Every tile beyond the roof span then went
/// unstable, caved in, and pulled the floor - and any bridge built on it - down from the level above.
///
/// This marker is therefore read from a position index maintained on component start, NOT from the anchored
/// lookup, so unanchored terrain still counts. It only affects cave-in roof stability; it does not make the
/// entity solid, walkable, or part of the above-ground support graph.
/// </summary>
[RegisterComponent]
public sealed partial class ZLoadBearingTerrainComponent : Component
{
}
