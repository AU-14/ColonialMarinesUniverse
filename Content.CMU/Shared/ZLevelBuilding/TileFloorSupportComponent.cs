// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): an invisible marker dropped on a laid floor tile (by <c>TileApplierSystem</c>)
/// so that the tile can participate in the structural-support graph. Tiles are not entities and cannot carry
/// <see cref="StructuralSupportComponent"/> themselves, so this tiny entity stands in for the tile: it sits on
/// the tile, carries the support component, and removes its underlying floor tile when it terminates (i.e. when
/// the support graph collapses it for lacking support on an upper z-level).
///
/// Only laid on UPPER z-levels - floors on the ground / underground rest on the ground and are inherently
/// stable, so there is no need to track them (and no per-tile entity cost there).
/// </summary>
[RegisterComponent]
public sealed partial class TileFloorSupportComponent : Component
{
}
