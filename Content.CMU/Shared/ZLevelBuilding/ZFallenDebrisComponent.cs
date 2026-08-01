// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.GameStates;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Marks a structure that fell through a collapsed z-level floor and is now rubble. The server strips its
/// collision entirely (so it can't block movement or bullets, and can't grind contacts against cave rocks)
/// and the client renders it battered: shrunken and darkened (see ZFallenDebrisVisualsSystem).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZFallenDebrisComponent : Component;
