// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// From the player: build this option in this pit.
[Serializable, NetSerializable]
public sealed class PitBuildEvent : EntityEventArgs
{
    public readonly NetEntity Pit;
    public readonly PitBuildOption Option;

    public PitBuildEvent(NetEntity pit, PitBuildOption option)
    {
        Pit = pit;
        Option = option;
    }
}
