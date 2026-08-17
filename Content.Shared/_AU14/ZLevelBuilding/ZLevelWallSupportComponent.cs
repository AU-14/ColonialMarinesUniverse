// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 CatoChristopherMrow
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Marks a wall as vertical support for the z-level directly above it without enrolling the wall itself in the
/// collapsible structural graph. Player-built walls additionally carry <see cref="StructuralSupportComponent"/>
/// so they can lose support and collapse like other constructed structures.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelWallSupportComponent : Component
{
    /// <summary>Fixed Manhattan reach projected onto the level above a wall.</summary>
    public const int CantileverSpan = 3;
}
