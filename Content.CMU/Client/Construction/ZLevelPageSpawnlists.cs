// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;

namespace Content.Client._AU14.Construction;

/// <summary>
/// Building overhaul (z-level): the spawnlists that belong to the separate "Z-Level (Experimental)" top-bar
/// page of the construction menu, as opposed to the normal Spawnlists page.
///
/// The two top-bar pages are distinct MAIN CATEGORIES and must not share their constructs: the z-level page
/// shows only these spawnlists (see <c>GmodConstructionMenu.BuildTree</c>), and conversely the "All"/AU14 view
/// on the Spawnlists page excludes them (see <c>ConstructionMenuPresenter.GetAndSortRecipes</c>) so z-level
/// constructs never leak into the normal "All" listing.
/// </summary>
public static class ZLevelPageSpawnlists
{
    /// <summary>The experimental z-level spawnlists: the buildable Tiles and the ZLevel stairs/beams.</summary>
    public static readonly string[] Names = { "Tiles", "ZLevel" };

    /// <summary>True if <paramref name="spawnlist"/> belongs to the separate z-level page.</summary>
    public static bool Contains(string spawnlist) => Array.IndexOf(Names, spawnlist) >= 0;
}
