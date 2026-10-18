// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// What can be built in a pit.
[Serializable, NetSerializable]
public enum PitBuildOption : byte
{
    Expand,

    Rope,

    Hatch,

    Ladder,

    Steps,
}
