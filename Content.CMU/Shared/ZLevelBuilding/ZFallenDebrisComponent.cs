// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Marks a structure that fell through a collapsed z-level floor and is now rubble. The server strips its
/// collision entirely (so it can't block movement or bullets, and can't grind contacts against cave rocks).
/// It looks exactly as it did before the fall (no filter on the sprite). It lands on the first solid tile below it
/// (see ZLandingSystem), and it can fall again, as often as the floor under it gives way.
/// RUMC was (kept): "...and the client renders it battered: shrunken and darkened (see ZFallenDebrisVisualsSystem)" - that system is switched off.
/// </summary>
[RegisterComponent, NetworkedComponent]
// RUMC: the component was empty (kept): public sealed partial class ZFallenDebrisComponent : Component;
public sealed partial class ZFallenDebrisComponent : Component
{
    // RUMC: the levels it has already fallen from, so that on one level a thing falls only once
    /// <summary>The levels it has already fallen from: on one level a thing falls only once.</summary>
    public HashSet<EntityUid> FellFrom = new();
}
