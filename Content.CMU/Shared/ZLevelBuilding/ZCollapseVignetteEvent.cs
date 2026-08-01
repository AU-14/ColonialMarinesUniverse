// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Server → client: a floor/cave collapse is happening near the player. Drives the client-side vignette:
/// a single dark-grey blink for a nearby collapse, or (while <see cref="Engulfed"/>) a rapid near-black
/// "eyes blinking" vignette that lasts as long as the cave keeps collapsing on the player - the server
/// re-sends this on its rumble cadence and the client extends the effect each time.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZCollapseVignetteEvent : EntityEventArgs
{
    /// <summary>True when the collapse region itself contains the player (the roof is coming down on them).</summary>
    public bool Engulfed;
}
