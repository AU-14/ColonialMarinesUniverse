// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): per-map opt-out switch for vertical building.
///
/// By DEFAULT the building overhaul treats <b>every</b> map as z-buildable - digging down lazily creates a
/// stone level below and links it into a CMU z-network on demand, so the feature works even on maps that
/// were authored as single-z (not multi-z). This component does not need to be added for the feature to work.
///
/// HOW TO MAKE A MAP "NOT MULTI-Z" (opt a map out of vertical building):
///   Add this component to the map prototype with <c>enabled: false</c>, e.g.
///
///       - type: entity
///         id: MyMapThatShouldStaySingleZ
///         ...
///         components:
///         - type: ZBuildableMap
///           enabled: false
///
///   With <c>enabled: false</c>, <see cref="Content.Server._AU14.ZLevelBuilding.ZLevelBuildingSystem"/> will
///   refuse to bootstrap a level below or generate stone for that map, leaving it strictly single-z.
///
/// To disable the feature globally in code (all maps), set
/// <c>ZLevelBuildingSystem.GloballyEnabled = false</c> instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZBuildableMapComponent : Component
{
    /// <summary>Whether this specific map allows the building overhaul's dig-down / vertical building.
    /// Networked so the client-side construction-ghost check (<see cref="ZBuildAllowed"/>) matches the server.</summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>Side length (in tiles) of each lazily-generated stone chunk on the level below.</summary>
    [DataField]
    public int ChunkSize = 8;

    /// <summary>
    /// Content tile prototypes used for the floor of generated stone levels. Generation picks one at random per
    /// tile, giving a stone/dirt mix. These are the CM planet tiles used on maps like Barker's Lament, not the
    /// vanilla SS14 asteroid tiles.
    /// </summary>
    [DataField]
    public List<string> StoneFloorTiles = new() { "CMPlanetMarsCave", "CMPlanetMarsDirt" };

    /// <summary>
    /// Mineable rock entity scattered across generated chunks (drops salvageable materials when mined).
    /// </summary>
    [DataField]
    public string StoneRockEntity = "mineablesolarisrocksteel";

    /// <summary>Loose rock debris flung outward at the moment a cave-in begins, before the roof buries the cavern.</summary>
    [DataField]
    public string RockDebris = "AU14RockDebris";

    /// <summary>See-through fog spawned across a cavern while it is caving in (atmosphere; does not block view).</summary>
    [DataField]
    public string CollapseFog = "AU14CollapseFog";

    /// <summary>Animated falling-rock effect spawned on each tile as it caves in.</summary>
    [DataField]
    public string CollapseRockProp = "AU14CollapseRockProp";

    /// <summary>
    /// UNDERGROUND roof stability: the maximum number of tiles any dug-out (open) tile may be from the nearest
    /// solid rock or built pillar before its roof is unstable and caves in. Mine a cavern wider than this with
    /// nothing holding the middle and it collapses; plant a pillar (a vertical <see cref="StructuralSupportComponent"/>)
    /// to make a bigger span safe - exactly like real mine support pillars.
    /// </summary>
    // Networked so the client structural-scanner heat-map uses the map's REAL span instead of assuming the
    // default (otherwise a tuned map would show "stable" where the server will collapse).
    [DataField, AutoNetworkedField]
    public int MaxRoofSpan = 3;

    /// <summary>Rumble/crash sound played to everyone on a level while a cave-in is collapsing.</summary>
    [DataField]
    public string RumbleSound = "/Audio/Effects/explosion3.ogg";
}
