// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): handheld structural scanner. Using it in hand toggles a CLIENT-SIDE roof-stability
/// heat-map overlay that is only drawn for the player actually holding an enabled scanner. The overlay shades
/// dug-out (open) tiles by how close they are to caving in, so a miner can see where a cavern needs a pillar
/// before it collapses.
///
/// Art note: this currently borrows an existing in-game item's sprite as a placeholder (no bespoke RSI yet).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StructuralScannerComponent : Component
{
    /// <summary>Whether the heat-map overlay is currently active for the holder.</summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;
}
