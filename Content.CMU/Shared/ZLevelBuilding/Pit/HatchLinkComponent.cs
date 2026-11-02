// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// Links a ladder (or rope) to the hatch that covers it.
[RegisterComponent]
public sealed partial class HatchLinkComponent : Component
{
    [ViewVariables]
    public EntityUid Hatch;
}
