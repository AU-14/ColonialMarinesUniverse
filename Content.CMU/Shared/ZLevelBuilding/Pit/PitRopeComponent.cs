// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// A rope hung in a pit, and the rope at the other end of it.
[RegisterComponent]
public sealed partial class PitRopeComponent : Component
{
    [ViewVariables]
    public EntityUid? Other;
}
