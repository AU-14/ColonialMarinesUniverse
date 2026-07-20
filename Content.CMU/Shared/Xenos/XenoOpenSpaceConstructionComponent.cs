// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared._AU14.Xenos;

/// <summary>
/// Marks a xeno structure as one that is built into a HOLE rather than onto a floor.
///
/// Normal xeno construction demands weeds and a solid, unblocked tile. A structure carrying this wants the
/// exact opposite: an empty tile, weeds or no weeds, because its whole purpose is patching a gap the floor
/// used to occupy. <c>SharedXenoConstructionSystem.CanSecreteOnTilePopup</c> inverts both checks when it sees
/// this component on the build choice.
/// </summary>
[RegisterComponent]
public sealed partial class XenoOpenSpaceConstructionComponent : Component
{
}
