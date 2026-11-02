// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Marks a thing that is part of the ground itself (a pit): when the floor under it collapses it is gone with the floor, and does not fall to the level below.
/// </summary>
[RegisterComponent]
public sealed partial class ZDestroyedOnCollapseComponent : Component;
