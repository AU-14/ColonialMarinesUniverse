// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

// Marks a thing on a tile that lets a falling body through (a hatch, a grate).
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUZLevelFallThroughComponent : Component
{
}
