// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// First building block of the z-level structural-stability feature. Marks a structure that participates
/// in the support graph: a tile is "supported" if it can trace a path back to an anchor (ground/bedrock) -
/// either directly, or by cantilevering out from a vertical support within <see cref="CantileverSpan"/>
/// tiles. The upcoming <c>ZLevelSupportSystem</c> recomputes <see cref="Supported"/> on build/destroy and
/// schedules a collapse (with an 8s warning) for anything that loses its path to an anchor.
/// </summary>
/// <remarks>
/// Per-material spans (wood &lt; metal &lt; plasteel) are expressed by setting <see cref="CantileverSpan"/>
/// on each buildable structure's prototype. <see cref="IsAnchor"/> roots the graph (the ground layer).
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StructuralSupportComponent : Component
{
    /// <summary>How many tiles a floor/structure may cantilever out from this support before it is unstable.</summary>
    [DataField, AutoNetworkedField]
    public int CantileverSpan = 2;

    /// <summary>If true this is a permanent ground anchor - always stable, and a root of the support graph.</summary>
    [DataField, AutoNetworkedField]
    public bool IsAnchor;

    /// <summary>
    /// If true this acts as a vertical support (a beam/column): when the graph reaches it, it relays its
    /// FULL <see cref="CantileverSpan"/> onward instead of decrementing the incoming budget. This lets a
    /// chain of beams extend buildable reach, and is what an upper-z floor will eventually "reflect" off of.
    /// Anchors implicitly behave as vertical supports.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsVerticalSupport;

    /// <summary>
    /// If true this support is hidden from the handheld structural scanner's "where can I build" heat-map.
    /// Used for the beam a staircase spawns with: that beam only holds up the staircase tiles, not general
    /// build ground, so shading its span green would wrongly tell players those tiles are buildable/supported.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HideFromScanner;

    /// <summary>Runtime: whether the support graph currently considers this entity supported. Not persisted.</summary>
    [ViewVariables]
    public bool Supported = true;
}
