// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// The key of the use of the item in the hand (Z) was pressed with a digging tool in the hand: dig the tile under the cursor. The server looks again at everything, the tool, the
// reach and the tile.
[Serializable, NetSerializable]
public sealed class PitDigRequestEvent : EntityEventArgs
{
    public readonly NetCoordinates Location;

    public PitDigRequestEvent(NetCoordinates location)
    {
        Location = location;
    }
}

// The climb out of a pit takes as long as the dig of it: the space bar begins it and this ends it.
[Serializable, NetSerializable]
public sealed partial class PitClimbDoAfterEvent : SimpleDoAfterEvent;
