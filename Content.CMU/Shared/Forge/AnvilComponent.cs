// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Forge;

// Marks an entity as an anvil, where a hot workpiece is struck into shape.
[RegisterComponent, NetworkedComponent]
public sealed partial class AnvilComponent : Component
{
}
