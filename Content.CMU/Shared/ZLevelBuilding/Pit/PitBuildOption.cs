// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

[Serializable, NetSerializable]
public enum PitBuildOption : byte
{
    Expand,

    Rope,

    Hatch,

    Ladder,

    Steps,

    // The pit is covered with a plate of the floor that has the code of a mine in it: it gives way again under whoever steps on it.
    MinePlate,
}
