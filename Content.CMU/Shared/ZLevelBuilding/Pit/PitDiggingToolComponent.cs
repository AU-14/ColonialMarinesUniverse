// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// A tool that digs pits: whether it digs up, and how fast.
[RegisterComponent]
public sealed partial class PitDiggingToolComponent : Component
{
    [DataField]
    public bool DigUp;

    // How long the dig takes, as a multiple of the usual time: a brass tool is slower than a steel one.
    [DataField]
    public float DigTimeMultiplier = 1f;
}
